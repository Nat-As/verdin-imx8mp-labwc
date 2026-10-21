// vk-scanout-test: check which Vulkan operations really write to a display
// (scanout) buffer on this GPU driver, and in which memory layout.
//
// Display buffers are allocated on the display (KMS) device in two ways:
//  - dumb: a DRM dumb buffer. Its CPU mapping is uncached (write-combined),
//    so reading it back shows exactly what the display controller scans out.
//  - gbm:  a linear GBM buffer object, as the wlroots DRM backend allocates
//    them. Its CPU mapping may be cached by the GPU driver, so a readback can
//    show stale data on some drivers.
// Each buffer is filled with a sentinel, imported into Vulkan the way the
// wlroots Vulkan renderer imports it (or bound to a VkBuffer), written with
// one of several Vulkan operations, and read back. ARGB8888 (UNORM) tests
// write a pattern that encodes each pixel's position, so a buffer written in
// a tiled layout is reported as such instead of as a generic failure.
//
// Usage: vk-scanout-test [-d /dev/dri/cardN] [-s WIDTHxHEIGHT] [-q] [-v]
//   -q  print only the recommended wlroots Vulkan mode: "direct" (renders
//       into the display buffer), "copy" (WLR_VK_RENDER_COPY=1), "buffer"
//       (WLR_VK_RENDER_BUFFER=1) or "none"
//   -v  print sample pixels of failed tests
// Exit status: 0 if a working mode was found, 1 if not, 2 on setup errors.

#define _GNU_SOURCE
#include <drm_fourcc.h>
#include <errno.h>
#include <fcntl.h>
#include <gbm.h>
#include <linux/dma-buf.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <sys/mman.h>
#include <unistd.h>
#include <vulkan/vulkan.h>
#include <xf86drm.h>
#include <xf86drmMode.h>

#include "fill_vert.h"
#include "fill_frag.h"
#include "pattern_frag.h"

#define SENTINEL 0x11223344u

static bool quiet, verbose;
#define info(...) do { if (!quiet) printf(__VA_ARGS__); } while (0)

struct vk {
	VkInstance instance;
	VkPhysicalDevice phdev;
	VkDevice dev;
	uint32_t family;
	VkQueue queue;
	VkCommandPool pool;
	bool has_foreign;
	PFN_vkGetMemoryFdPropertiesKHR get_memory_fd_properties;
};

enum kind { DUMB, GBM };

struct scanout {
	enum kind kind;
	int kms_fd;
	int fd; // DMA-BUF
	uint32_t width, height, stride, offset;
	uint64_t modifier;
	uint32_t *map;
	size_t map_size;
	struct gbm_bo *bo;
	uint32_t dumb_handle;
};

struct target {
	VkImage image;
	VkBuffer buffer;
	VkDeviceMemory memory;
	VkImageView view;
};

enum method {
	M_DRAW,           // wlroots default: draw into the DMA-BUF image
	M_DRAW_NOFOREIGN, // same without queue family ownership transfers
	M_CLEAR_PASS,     // render pass with loadOp CLEAR
	M_CLEAR_CMD,      // vkCmdClearColorImage
	M_DRAW_COPY,      // draw into an optimal image, vkCmdCopyImage (WLR_VK_RENDER_COPY)
	M_DRAW_BLIT,      // draw into an optimal image, vkCmdBlitImage
	M_BUFFER_COPY,    // vkCmdCopyBufferToImage from a host buffer
	M_LINEAR_DRAW,    // DMA-BUF imported with VK_IMAGE_TILING_LINEAR, draw
	M_TO_BUFFER,      // draw into an optimal image, vkCmdCopyImageToBuffer into
	                  // a VkBuffer bound to the DMA-BUF (WLR_VK_RENDER_BUFFER)
	M_COUNT,
};

static const char *method_names[M_COUNT] = {
	"draw (wlroots default)",
	"draw, no FOREIGN barrier",
	"render pass clear",
	"vkCmdClearColorImage",
	"draw + copy (WLR_VK_RENDER_COPY)",
	"draw + blit",
	"buffer -> image copy",
	"draw, image with LINEAR tiling",
	"draw + copy to VkBuffer (WLR_VK_RENDER_BUFFER)",
};

enum outcome { OUT_OK, OUT_NOT_WRITTEN, OUT_TILED, OUT_WRONG, OUT_FAILED };

static void check(VkResult res, const char *what) {
	if (res != VK_SUCCESS) {
		fprintf(stderr, "vk-scanout-test: %s failed: %d\n", what, res);
		exit(2);
	}
}

static bool has_ext(const VkExtensionProperties *exts, uint32_t n, const char *name) {
	for (uint32_t i = 0; i < n; i++) {
		if (strcmp(exts[i].extensionName, name) == 0) {
			return true;
		}
	}
	return false;
}

static void vk_init(struct vk *vk) {
	VkApplicationInfo app = {
		.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO,
		.pApplicationName = "vk-scanout-test",
		.apiVersion = VK_API_VERSION_1_1,
	};
	VkInstanceCreateInfo ici = {
		.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
		.pApplicationInfo = &app,
	};
	check(vkCreateInstance(&ici, NULL, &vk->instance), "vkCreateInstance");

	uint32_t n = 1;
	VkResult res = vkEnumeratePhysicalDevices(vk->instance, &n, &vk->phdev);
	if ((res != VK_SUCCESS && res != VK_INCOMPLETE) || n == 0) {
		fprintf(stderr, "vk-scanout-test: no Vulkan device\n");
		exit(2);
	}
	VkPhysicalDeviceProperties props;
	vkGetPhysicalDeviceProperties(vk->phdev, &props);
	info("Vulkan device: %s, API %u.%u.%u, driver 0x%x\n", props.deviceName,
		VK_API_VERSION_MAJOR(props.apiVersion), VK_API_VERSION_MINOR(props.apiVersion),
		VK_API_VERSION_PATCH(props.apiVersion), props.driverVersion);

	uint32_t next = 0;
	vkEnumerateDeviceExtensionProperties(vk->phdev, NULL, &next, NULL);
	VkExtensionProperties *exts = calloc(next, sizeof(*exts));
	vkEnumerateDeviceExtensionProperties(vk->phdev, NULL, &next, exts);
	const char *wanted[] = {
		VK_KHR_EXTERNAL_MEMORY_FD_EXTENSION_NAME,
		VK_EXT_EXTERNAL_MEMORY_DMA_BUF_EXTENSION_NAME,
		VK_EXT_IMAGE_DRM_FORMAT_MODIFIER_EXTENSION_NAME,
		VK_EXT_QUEUE_FAMILY_FOREIGN_EXTENSION_NAME,
		VK_KHR_IMAGE_FORMAT_LIST_EXTENSION_NAME,
	};
	const char *enabled[8];
	uint32_t enabled_len = 0;
	for (size_t i = 0; i < sizeof(wanted) / sizeof(wanted[0]); i++) {
		bool ok = has_ext(exts, next, wanted[i]);
		info("  %-36s %s\n", wanted[i], ok ? "yes" : "NO");
		if (ok) {
			enabled[enabled_len++] = wanted[i];
		} else if (i < 3) {
			fprintf(stderr, "vk-scanout-test: required extension %s missing\n", wanted[i]);
			exit(2);
		}
	}
	vk->has_foreign = has_ext(exts, next, VK_EXT_QUEUE_FAMILY_FOREIGN_EXTENSION_NAME);
	free(exts);

	uint32_t nq = 0;
	vkGetPhysicalDeviceQueueFamilyProperties(vk->phdev, &nq, NULL);
	VkQueueFamilyProperties *qf = calloc(nq, sizeof(*qf));
	vkGetPhysicalDeviceQueueFamilyProperties(vk->phdev, &nq, qf);
	vk->family = UINT32_MAX;
	for (uint32_t i = 0; i < nq; i++) {
		if (qf[i].queueFlags & VK_QUEUE_GRAPHICS_BIT) {
			vk->family = i;
			break;
		}
	}
	free(qf);
	if (vk->family == UINT32_MAX) {
		fprintf(stderr, "vk-scanout-test: no graphics queue\n");
		exit(2);
	}

	float prio = 1.0f;
	VkDeviceQueueCreateInfo qci = {
		.sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
		.queueFamilyIndex = vk->family,
		.queueCount = 1,
		.pQueuePriorities = &prio,
	};
	VkDeviceCreateInfo dci = {
		.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
		.queueCreateInfoCount = 1,
		.pQueueCreateInfos = &qci,
		.enabledExtensionCount = enabled_len,
		.ppEnabledExtensionNames = enabled,
	};
	check(vkCreateDevice(vk->phdev, &dci, NULL, &vk->dev), "vkCreateDevice");
	vkGetDeviceQueue(vk->dev, vk->family, 0, &vk->queue);
	vk->get_memory_fd_properties = (PFN_vkGetMemoryFdPropertiesKHR)
		vkGetDeviceProcAddr(vk->dev, "vkGetMemoryFdPropertiesKHR");

	VkCommandPoolCreateInfo pci = {
		.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
		.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
		.queueFamilyIndex = vk->family,
	};
	check(vkCreateCommandPool(vk->dev, &pci, NULL, &vk->pool), "vkCreateCommandPool");
}

static void print_format(struct vk *vk, VkFormat format, const char *name) {
	VkDrmFormatModifierPropertiesListEXT list = {
		.sType = VK_STRUCTURE_TYPE_DRM_FORMAT_MODIFIER_PROPERTIES_LIST_EXT,
	};
	VkFormatProperties2 props = {
		.sType = VK_STRUCTURE_TYPE_FORMAT_PROPERTIES_2,
		.pNext = &list,
	};
	vkGetPhysicalDeviceFormatProperties2(vk->phdev, format, &props);
	VkDrmFormatModifierPropertiesEXT *mods = calloc(list.drmFormatModifierCount + 1, sizeof(*mods));
	list.pDrmFormatModifierProperties = mods;
	vkGetPhysicalDeviceFormatProperties2(vk->phdev, format, &props);
	info("%s:\n", name);
	VkFormatFeatureFlags lin = props.formatProperties.linearTilingFeatures;
	info("  VK_IMAGE_TILING_LINEAR       color-attachment=%d blit-dst=%d transfer-dst=%d\n",
		!!(lin & VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT),
		!!(lin & VK_FORMAT_FEATURE_BLIT_DST_BIT),
		!!(lin & VK_FORMAT_FEATURE_TRANSFER_DST_BIT));
	for (uint32_t i = 0; i < list.drmFormatModifierCount; i++) {
		VkFormatFeatureFlags f = mods[i].drmFormatModifierTilingFeatures;
		char *mname = drmGetFormatModifierName(mods[i].drmFormatModifier);
		info("  modifier %-19s color-attachment=%d blit-dst=%d transfer-dst=%d sampled=%d\n",
			mname ? mname : "?",
			!!(f & VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT),
			!!(f & VK_FORMAT_FEATURE_BLIT_DST_BIT),
			!!(f & VK_FORMAT_FEATURE_TRANSFER_DST_BIT),
			!!(f & VK_FORMAT_FEATURE_SAMPLED_IMAGE_BIT));
		free(mname);
	}
	free(mods);
}

static int find_mem_type(struct vk *vk, uint32_t bits, VkMemoryPropertyFlags flags) {
	VkPhysicalDeviceMemoryProperties mp;
	vkGetPhysicalDeviceMemoryProperties(vk->phdev, &mp);
	for (uint32_t i = 0; i < mp.memoryTypeCount; i++) {
		if ((bits & (1u << i)) && (mp.memoryTypes[i].propertyFlags & flags) == flags) {
			return (int)i;
		}
	}
	return -1;
}

// --- Display buffers ------------------------------------------------------

static bool scanout_create(enum kind kind, int kms_fd, struct gbm_device *gbm,
		uint32_t format, uint32_t w, uint32_t h, struct scanout *s) {
	memset(s, 0, sizeof(*s));
	s->kind = kind;
	s->kms_fd = kms_fd;
	s->width = w;
	s->height = h;
	s->fd = -1;
	if (kind == DUMB) {
		struct drm_mode_create_dumb create = { .width = w, .height = h, .bpp = 32 };
		if (drmIoctl(kms_fd, DRM_IOCTL_MODE_CREATE_DUMB, &create) != 0) {
			info("  dumb buffer: DRM_IOCTL_MODE_CREATE_DUMB failed: %s\n", strerror(errno));
			return false;
		}
		s->dumb_handle = create.handle;
		s->stride = create.pitch;
		s->modifier = DRM_FORMAT_MOD_LINEAR;
		if (drmPrimeHandleToFD(kms_fd, create.handle, DRM_CLOEXEC | DRM_RDWR, &s->fd) != 0) {
			info("  dumb buffer: PRIME export failed: %s\n", strerror(errno));
			return false;
		}
		struct drm_mode_map_dumb map = { .handle = create.handle };
		if (drmIoctl(kms_fd, DRM_IOCTL_MODE_MAP_DUMB, &map) != 0) {
			info("  dumb buffer: DRM_IOCTL_MODE_MAP_DUMB failed: %s\n", strerror(errno));
			return false;
		}
		s->map_size = create.size;
		s->map = mmap(NULL, s->map_size, PROT_READ | PROT_WRITE, MAP_SHARED, kms_fd,
			map.offset);
	} else {
		uint64_t linear = DRM_FORMAT_MOD_LINEAR;
		s->bo = gbm_bo_create_with_modifiers(gbm, w, h, format, &linear, 1);
		if (s->bo == NULL) {
			s->bo = gbm_bo_create(gbm, w, h, format,
				GBM_BO_USE_SCANOUT | GBM_BO_USE_RENDERING | GBM_BO_USE_LINEAR);
		}
		if (s->bo == NULL) {
			info("  GBM buffer: gbm_bo_create failed: %s\n", strerror(errno));
			return false;
		}
		s->fd = gbm_bo_get_fd(s->bo);
		s->stride = gbm_bo_get_stride_for_plane(s->bo, 0);
		s->offset = gbm_bo_get_offset(s->bo, 0);
		s->modifier = gbm_bo_get_modifier(s->bo);
		s->map_size = (size_t)s->offset + (size_t)s->stride * h;
		s->map = mmap(NULL, s->map_size, PROT_READ | PROT_WRITE, MAP_SHARED, s->fd, 0);
	}
	if (s->map == MAP_FAILED) {
		info("  mmap failed: %s\n", strerror(errno));
		s->map = NULL;
		return false;
	}
	return true;
}

static void scanout_destroy(struct scanout *s) {
	if (s->map) {
		munmap(s->map, s->map_size);
	}
	if (s->fd >= 0) {
		close(s->fd);
	}
	if (s->bo) {
		gbm_bo_destroy(s->bo);
	}
	if (s->dumb_handle) {
		struct drm_mode_destroy_dumb destroy = { .handle = s->dumb_handle };
		drmIoctl(s->kms_fd, DRM_IOCTL_MODE_DESTROY_DUMB, &destroy);
	}
}

static void scanout_sync(struct scanout *s, uint64_t flags) {
	struct dma_buf_sync sync = { .flags = flags };
	ioctl(s->fd, DMA_BUF_IOCTL_SYNC, &sync);
}

static uint32_t *row(struct scanout *s, uint32_t y) {
	return (uint32_t *)((char *)s->map + s->offset + (size_t)y * s->stride);
}

static void scanout_fill(struct scanout *s, uint32_t value) {
	scanout_sync(s, DMA_BUF_SYNC_START | DMA_BUF_SYNC_WRITE);
	for (uint32_t y = 0; y < s->height; y++) {
		uint32_t *r = row(s, y);
		for (uint32_t x = 0; x < s->width; x++) {
			r[x] = value;
		}
	}
	scanout_sync(s, DMA_BUF_SYNC_END | DMA_BUF_SYNC_WRITE);
}

// Position-encoding pattern (see pattern.frag)
static uint32_t pattern(uint32_t x, uint32_t y) {
	return 0xff000000u | (x & 255) << 16 | (y & 255) << 8 |
		((x >> 8) & 15) | ((y >> 8) & 15) << 4;
}

static void decode(uint32_t w, uint32_t *x, uint32_t *y) {
	*x = ((w >> 16) & 255) | (w & 15) << 8;
	*y = ((w >> 8) & 255) | ((w >> 4) & 15) << 8;
}

// Analyse the buffer: the share of pixels with the expected content at their
// place, still holding the sentinel, and (for the pattern) at the place a
// 4x4-tiled layout would put them
static enum outcome scanout_check(struct scanout *s, bool use_pattern, char *msg,
		size_t msg_size) {
	size_t total = (size_t)s->width * s->height;
	size_t n_ok = 0, n_sentinel = 0, n_pattern = 0, n_tiled = 0;
	uint32_t pitch = s->stride / 4;
	scanout_sync(s, DMA_BUF_SYNC_START | DMA_BUF_SYNC_READ);
	for (uint32_t y = 0; y < s->height; y++) {
		const uint32_t *r = row(s, y);
		for (uint32_t x = 0; x < s->width; x++) {
			uint32_t w = r[x];
			if (w == SENTINEL) {
				n_sentinel++;
				continue;
			}
			if (!use_pattern) {
				n_ok += (w & 0x00ffffffu) == 0x00ffffffu;
				continue;
			}
			if (w == pattern(x, y)) {
				n_ok++;
			}
			uint32_t px, py;
			decode(w, &px, &py);
			if ((w >> 24) == 0xff && px < s->width && py < s->height) {
				n_pattern++;
				// Word index of (px, py) in a 4x4-tiled layout with the
				// same pitch
				size_t tiled = (size_t)(py >> 2) * pitch * 4 + (px >> 2) * 16 +
					(py & 3) * 4 + (px & 3);
				n_tiled += tiled == (size_t)y * pitch + x;
			}
		}
	}
	scanout_sync(s, DMA_BUF_SYNC_END | DMA_BUF_SYNC_READ);

	double ok = (double)n_ok / total, sentinel = (double)n_sentinel / total;
	if (ok > 0.999) {
		snprintf(msg, msg_size, "OK (%s, linear)", use_pattern ? "pattern" : "white");
		return OUT_OK;
	}
	if (sentinel > 0.999) {
		snprintf(msg, msg_size, "NOT WRITTEN (memory unchanged)");
		return OUT_NOT_WRITTEN;
	}
	if (use_pattern && (double)n_tiled / total > 0.99) {
		snprintf(msg, msg_size, "WRITTEN IN 4x4-TILED LAYOUT (pixels in the wrong place)");
		return OUT_TILED;
	}
	if (use_pattern && (double)n_pattern / total > 0.9) {
		snprintf(msg, msg_size,
			"WRITTEN IN AN UNKNOWN LAYOUT (%.0f%% at the right place; pixels in the wrong place)",
			ok * 100);
		return OUT_TILED;
	}
	snprintf(msg, msg_size, "WRONG (%.0f%% right, %.0f%% unchanged)", ok * 100, sentinel * 100);
	return OUT_WRONG;
}

static void scanout_dump(struct scanout *s) {
	printf("      first pixels, decoded as (x,y) of the pattern:\n");
	for (uint32_t y = 0; y < 4 && y < s->height; y++) {
		printf("      row %u:", y);
		const uint32_t *r = row(s, y);
		for (uint32_t x = 0; x < 8 && x < s->width; x++) {
			if (r[x] == SENTINEL) {
				printf(" ------");
			} else {
				uint32_t px, py;
				decode(r[x], &px, &py);
				printf(" %u,%u", px, py);
			}
		}
		printf("\n");
	}
}

// --- Vulkan targets ---------------------------------------------------------

static bool import_memory(struct vk *vk, struct scanout *s, VkMemoryRequirements *req,
		VkImage image, VkBuffer buffer, VkDeviceMemory *memory) {
	VkMemoryFdPropertiesKHR fdp = { .sType = VK_STRUCTURE_TYPE_MEMORY_FD_PROPERTIES_KHR };
	VkResult res = vk->get_memory_fd_properties(vk->dev,
		VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT, s->fd, &fdp);
	if (res != VK_SUCCESS) {
		info("    vkGetMemoryFdPropertiesKHR failed: %d\n", res);
		return false;
	}
	int type = find_mem_type(vk, req->memoryTypeBits & fdp.memoryTypeBits, 0);
	if (type < 0) {
		info("    no memory type for the DMA-BUF\n");
		return false;
	}
	VkMemoryDedicatedAllocateInfo dedicated = {
		.sType = VK_STRUCTURE_TYPE_MEMORY_DEDICATED_ALLOCATE_INFO,
		.image = image,
		.buffer = buffer,
	};
	VkImportMemoryFdInfoKHR import = {
		.sType = VK_STRUCTURE_TYPE_IMPORT_MEMORY_FD_INFO_KHR,
		.pNext = &dedicated,
		.handleType = VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT,
		.fd = fcntl(s->fd, F_DUPFD_CLOEXEC, 0),
	};
	VkMemoryAllocateInfo mai = {
		.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
		.pNext = &import,
		.allocationSize = req->size,
		.memoryTypeIndex = (uint32_t)type,
	};
	res = vkAllocateMemory(vk->dev, &mai, NULL, memory);
	if (res != VK_SUCCESS) {
		info("    vkAllocateMemory (DMA-BUF import) failed: %d\n", res);
		close(import.fd);
		return false;
	}
	return true;
}

// Import the display buffer as a VkImage: with a DRM format modifier like
// wlroots, or with VK_IMAGE_TILING_LINEAR
static bool import_image(struct vk *vk, struct scanout *s, VkFormat format, bool linear_tiling,
		struct target *t, char *msg, size_t msg_size) {
	VkExternalMemoryImageCreateInfo emi = {
		.sType = VK_STRUCTURE_TYPE_EXTERNAL_MEMORY_IMAGE_CREATE_INFO,
		.handleTypes = VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT,
	};
	VkSubresourceLayout layout = { .offset = s->offset, .rowPitch = s->stride };
	VkImageDrmFormatModifierExplicitCreateInfoEXT mod = {
		.sType = VK_STRUCTURE_TYPE_IMAGE_DRM_FORMAT_MODIFIER_EXPLICIT_CREATE_INFO_EXT,
		.drmFormatModifier = s->modifier == DRM_FORMAT_MOD_INVALID ?
			DRM_FORMAT_MOD_LINEAR : s->modifier,
		.drmFormatModifierPlaneCount = 1,
		.pPlaneLayouts = &layout,
	};
	if (!linear_tiling) {
		emi.pNext = &mod;
	}
	VkImageCreateInfo ici = {
		.sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
		.pNext = &emi,
		.imageType = VK_IMAGE_TYPE_2D,
		.format = format,
		.extent = { s->width, s->height, 1 },
		.mipLevels = 1,
		.arrayLayers = 1,
		.samples = VK_SAMPLE_COUNT_1_BIT,
		.tiling = linear_tiling ? VK_IMAGE_TILING_LINEAR :
			VK_IMAGE_TILING_DRM_FORMAT_MODIFIER_EXT,
		.usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_SRC_BIT |
			VK_IMAGE_USAGE_TRANSFER_DST_BIT,
		.sharingMode = VK_SHARING_MODE_EXCLUSIVE,
		.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED,
	};
	VkResult res = vkCreateImage(vk->dev, &ici, NULL, &t->image);
	if (res != VK_SUCCESS) {
		snprintf(msg, msg_size, "FAILED (vkCreateImage: %d)", res);
		return false;
	}
	if (linear_tiling) {
		VkImageSubresource sub = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0 };
		VkSubresourceLayout l;
		vkGetImageSubresourceLayout(vk->dev, t->image, &sub, &l);
		if (l.rowPitch != s->stride || l.offset != 0) {
			snprintf(msg, msg_size, "FAILED (driver row pitch %llu, buffer stride %u)",
				(unsigned long long)l.rowPitch, s->stride);
			return false;
		}
	}
	VkMemoryRequirements req;
	vkGetImageMemoryRequirements(vk->dev, t->image, &req);
	if (!import_memory(vk, s, &req, t->image, VK_NULL_HANDLE, &t->memory)) {
		snprintf(msg, msg_size, "FAILED (DMA-BUF import)");
		return false;
	}
	check(vkBindImageMemory(vk->dev, t->image, t->memory, linear_tiling ? s->offset : 0),
		"vkBindImageMemory");
	return true;
}

static bool import_buffer(struct vk *vk, struct scanout *s, struct target *t,
		char *msg, size_t msg_size) {
	VkExternalMemoryBufferCreateInfo emb = {
		.sType = VK_STRUCTURE_TYPE_EXTERNAL_MEMORY_BUFFER_CREATE_INFO,
		.handleTypes = VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT,
	};
	VkBufferCreateInfo bci = {
		.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
		.pNext = &emb,
		.size = (VkDeviceSize)s->offset + (VkDeviceSize)s->stride * s->height,
		.usage = VK_BUFFER_USAGE_TRANSFER_DST_BIT | VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
		.sharingMode = VK_SHARING_MODE_EXCLUSIVE,
	};
	VkResult res = vkCreateBuffer(vk->dev, &bci, NULL, &t->buffer);
	if (res != VK_SUCCESS) {
		snprintf(msg, msg_size, "FAILED (vkCreateBuffer: %d)", res);
		return false;
	}
	VkMemoryRequirements req;
	vkGetBufferMemoryRequirements(vk->dev, t->buffer, &req);
	if (!import_memory(vk, s, &req, VK_NULL_HANDLE, t->buffer, &t->memory)) {
		snprintf(msg, msg_size, "FAILED (DMA-BUF import)");
		return false;
	}
	check(vkBindBufferMemory(vk->dev, t->buffer, t->memory, 0), "vkBindBufferMemory");
	return true;
}

static void create_optimal(struct vk *vk, VkFormat format, uint32_t w, uint32_t h,
		struct target *t) {
	VkImageCreateInfo ici = {
		.sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
		.imageType = VK_IMAGE_TYPE_2D,
		.format = format,
		.extent = { w, h, 1 },
		.mipLevels = 1,
		.arrayLayers = 1,
		.samples = VK_SAMPLE_COUNT_1_BIT,
		.tiling = VK_IMAGE_TILING_OPTIMAL,
		.usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_SRC_BIT,
		.sharingMode = VK_SHARING_MODE_EXCLUSIVE,
		.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED,
	};
	check(vkCreateImage(vk->dev, &ici, NULL, &t->image), "vkCreateImage (optimal)");
	VkMemoryRequirements req;
	vkGetImageMemoryRequirements(vk->dev, t->image, &req);
	int type = find_mem_type(vk, req.memoryTypeBits, VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
	VkMemoryAllocateInfo mai = {
		.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
		.allocationSize = req.size,
		.memoryTypeIndex = (uint32_t)(type < 0 ? 0 : type),
	};
	check(vkAllocateMemory(vk->dev, &mai, NULL, &t->memory), "vkAllocateMemory (optimal)");
	check(vkBindImageMemory(vk->dev, t->image, t->memory, 0), "vkBindImageMemory (optimal)");
}

static void create_view(struct vk *vk, VkFormat format, struct target *t) {
	VkImageViewCreateInfo vci = {
		.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
		.image = t->image,
		.viewType = VK_IMAGE_VIEW_TYPE_2D,
		.format = format,
		.subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 },
	};
	check(vkCreateImageView(vk->dev, &vci, NULL, &t->view), "vkCreateImageView");
}

static void destroy_target(struct vk *vk, struct target *t) {
	if (t->view) {
		vkDestroyImageView(vk->dev, t->view, NULL);
	}
	if (t->image) {
		vkDestroyImage(vk->dev, t->image, NULL);
	}
	if (t->buffer) {
		vkDestroyBuffer(vk->dev, t->buffer, NULL);
	}
	if (t->memory) {
		vkFreeMemory(vk->dev, t->memory, NULL);
	}
	memset(t, 0, sizeof(*t));
}

// --- Rendering ------------------------------------------------------------

// The render pass of wlroots' Vulkan renderer for formats without a blend
// image: one color attachment in the GENERAL layout, loadOp LOAD
struct pass {
	VkRenderPass render_pass;
	VkPipelineLayout layout;
	VkPipeline pipeline;
};

static VkShaderModule shader(struct vk *vk, const uint32_t *code, size_t size) {
	VkShaderModuleCreateInfo sci = {
		.sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO,
		.codeSize = size,
		.pCode = code,
	};
	VkShaderModule m;
	check(vkCreateShaderModule(vk->dev, &sci, NULL, &m), "vkCreateShaderModule");
	return m;
}

static void create_pass(struct vk *vk, VkFormat format, bool clear, bool use_pattern,
		uint32_t w, uint32_t h, struct pass *p) {
	VkAttachmentDescription att = {
		.format = format,
		.samples = VK_SAMPLE_COUNT_1_BIT,
		.loadOp = clear ? VK_ATTACHMENT_LOAD_OP_CLEAR : VK_ATTACHMENT_LOAD_OP_LOAD,
		.storeOp = VK_ATTACHMENT_STORE_OP_STORE,
		.stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE,
		.stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE,
		.initialLayout = VK_IMAGE_LAYOUT_GENERAL,
		.finalLayout = VK_IMAGE_LAYOUT_GENERAL,
	};
	VkAttachmentReference ref = { 0, VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL };
	VkSubpassDescription sub = {
		.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS,
		.colorAttachmentCount = 1,
		.pColorAttachments = &ref,
	};
	VkRenderPassCreateInfo rpi = {
		.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO,
		.attachmentCount = 1,
		.pAttachments = &att,
		.subpassCount = 1,
		.pSubpasses = &sub,
	};
	check(vkCreateRenderPass(vk->dev, &rpi, NULL, &p->render_pass), "vkCreateRenderPass");

	VkPipelineLayoutCreateInfo pli = { .sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO };
	check(vkCreatePipelineLayout(vk->dev, &pli, NULL, &p->layout), "vkCreatePipelineLayout");

	VkShaderModule vs = shader(vk, fill_vert, sizeof(fill_vert));
	VkShaderModule fs = use_pattern ? shader(vk, pattern_frag, sizeof(pattern_frag)) :
		shader(vk, fill_frag, sizeof(fill_frag));
	VkPipelineShaderStageCreateInfo stages[2] = {
		{ .sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
		  .stage = VK_SHADER_STAGE_VERTEX_BIT, .module = vs, .pName = "main" },
		{ .sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
		  .stage = VK_SHADER_STAGE_FRAGMENT_BIT, .module = fs, .pName = "main" },
	};
	VkPipelineVertexInputStateCreateInfo vi = {
		.sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO,
	};
	VkPipelineInputAssemblyStateCreateInfo ia = {
		.sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO,
		.topology = VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST,
	};
	VkViewport viewport = { 0, 0, (float)w, (float)h, 0, 1 };
	VkRect2D scissor = { { 0, 0 }, { w, h } };
	VkPipelineViewportStateCreateInfo vp = {
		.sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO,
		.viewportCount = 1, .pViewports = &viewport,
		.scissorCount = 1, .pScissors = &scissor,
	};
	VkPipelineRasterizationStateCreateInfo rs = {
		.sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
		.polygonMode = VK_POLYGON_MODE_FILL,
		.cullMode = VK_CULL_MODE_NONE,
		.frontFace = VK_FRONT_FACE_COUNTER_CLOCKWISE,
		.lineWidth = 1.0f,
	};
	VkPipelineMultisampleStateCreateInfo ms = {
		.sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
		.rasterizationSamples = VK_SAMPLE_COUNT_1_BIT,
	};
	// Premultiplied alpha blending, as in wlroots
	VkPipelineColorBlendAttachmentState blend_att = {
		.blendEnable = VK_TRUE,
		.srcColorBlendFactor = VK_BLEND_FACTOR_ONE,
		.dstColorBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA,
		.colorBlendOp = VK_BLEND_OP_ADD,
		.srcAlphaBlendFactor = VK_BLEND_FACTOR_ONE,
		.dstAlphaBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA,
		.alphaBlendOp = VK_BLEND_OP_ADD,
		.colorWriteMask = 0xf,
	};
	VkPipelineColorBlendStateCreateInfo cb = {
		.sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO,
		.attachmentCount = 1,
		.pAttachments = &blend_att,
	};
	VkGraphicsPipelineCreateInfo gpi = {
		.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO,
		.stageCount = 2,
		.pStages = stages,
		.pVertexInputState = &vi,
		.pInputAssemblyState = &ia,
		.pViewportState = &vp,
		.pRasterizationState = &rs,
		.pMultisampleState = &ms,
		.pColorBlendState = &cb,
		.layout = p->layout,
		.renderPass = p->render_pass,
	};
	check(vkCreateGraphicsPipelines(vk->dev, VK_NULL_HANDLE, 1, &gpi, NULL, &p->pipeline),
		"vkCreateGraphicsPipelines");
	vkDestroyShaderModule(vk->dev, vs, NULL);
	vkDestroyShaderModule(vk->dev, fs, NULL);
}

static void destroy_pass(struct vk *vk, struct pass *p) {
	vkDestroyPipeline(vk->dev, p->pipeline, NULL);
	vkDestroyPipelineLayout(vk->dev, p->layout, NULL);
	vkDestroyRenderPass(vk->dev, p->render_pass, NULL);
}

static void image_barrier(VkCommandBuffer cb, VkImage image, VkImageLayout from,
		VkImageLayout to, uint32_t src_family, uint32_t dst_family,
		VkAccessFlags src_access, VkAccessFlags dst_access) {
	VkImageMemoryBarrier b = {
		.sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
		.srcAccessMask = src_access,
		.dstAccessMask = dst_access,
		.oldLayout = from,
		.newLayout = to,
		.srcQueueFamilyIndex = src_family,
		.dstQueueFamilyIndex = dst_family,
		.image = image,
		.subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 },
	};
	vkCmdPipelineBarrier(cb, VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,
		VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, 0, 0, NULL, 0, NULL, 1, &b);
}

static void buffer_barrier(VkCommandBuffer cb, VkBuffer buffer, uint32_t src_family,
		uint32_t dst_family, VkAccessFlags src_access, VkAccessFlags dst_access) {
	VkBufferMemoryBarrier b = {
		.sType = VK_STRUCTURE_TYPE_BUFFER_MEMORY_BARRIER,
		.srcAccessMask = src_access,
		.dstAccessMask = dst_access,
		.srcQueueFamilyIndex = src_family,
		.dstQueueFamilyIndex = dst_family,
		.buffer = buffer,
		.size = VK_WHOLE_SIZE,
	};
	vkCmdPipelineBarrier(cb, VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,
		VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, 0, 0, NULL, 1, &b, 0, NULL);
}

// Records the render pass; returns the framebuffer, which the caller destroys
// once the command buffer has completed
static VkFramebuffer draw(struct vk *vk, VkCommandBuffer cb, struct pass *p,
		struct target *t, uint32_t w, uint32_t h, bool clear) {
	VkFramebufferCreateInfo fci = {
		.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO,
		.renderPass = p->render_pass,
		.attachmentCount = 1,
		.pAttachments = &t->view,
		.width = w,
		.height = h,
		.layers = 1,
	};
	VkFramebuffer fb;
	check(vkCreateFramebuffer(vk->dev, &fci, NULL, &fb), "vkCreateFramebuffer");
	VkClearValue white = { .color = { .float32 = { 1, 1, 1, 1 } } };
	VkRenderPassBeginInfo rbi = {
		.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO,
		.renderPass = p->render_pass,
		.framebuffer = fb,
		.renderArea = { { 0, 0 }, { w, h } },
		.clearValueCount = clear ? 1 : 0,
		.pClearValues = clear ? &white : NULL,
	};
	vkCmdBeginRenderPass(cb, &rbi, VK_SUBPASS_CONTENTS_INLINE);
	if (!clear) {
		vkCmdBindPipeline(cb, VK_PIPELINE_BIND_POINT_GRAPHICS, p->pipeline);
		vkCmdDraw(cb, 3, 1, 0, 0);
	}
	vkCmdEndRenderPass(cb);
	return fb;
}

static enum outcome run_method(struct vk *vk, struct scanout *s, VkFormat format,
		bool unorm, enum method m, char *msg, size_t msg_size) {
	uint32_t w = s->width, h = s->height;
	uint32_t foreign = vk->has_foreign ? VK_QUEUE_FAMILY_FOREIGN_EXT : VK_QUEUE_FAMILY_IGNORED;
	struct target dst = {0}, src = {0};
	struct pass pass = {0};
	VkFramebuffer fb = VK_NULL_HANDLE;
	VkBuffer host_buf = VK_NULL_HANDLE;
	VkDeviceMemory host_mem = VK_NULL_HANDLE;
	// The position pattern needs exact color values: UNORM only (sRGB
	// encoding changes them), and not for the clears
	bool use_pattern = m == M_BUFFER_COPY ||
		(unorm && m != M_CLEAR_PASS && m != M_CLEAR_CMD);
	enum outcome result = OUT_FAILED;

	scanout_fill(s, SENTINEL);
	bool ok = m == M_TO_BUFFER ? import_buffer(vk, s, &dst, msg, msg_size) :
		import_image(vk, s, format, m == M_LINEAR_DRAW, &dst, msg, msg_size);
	if (!ok) {
		destroy_target(vk, &dst);
		return OUT_FAILED;
	}

	VkCommandBufferAllocateInfo cai = {
		.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
		.commandPool = vk->pool,
		.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY,
		.commandBufferCount = 1,
	};
	VkCommandBuffer cb;
	check(vkAllocateCommandBuffers(vk->dev, &cai, &cb), "vkAllocateCommandBuffers");
	VkCommandBufferBeginInfo cbi = {
		.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
		.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT,
	};
	check(vkBeginCommandBuffer(cb, &cbi), "vkBeginCommandBuffer");

	bool use_foreign = m != M_DRAW_NOFOREIGN;
	uint32_t from = use_foreign ? foreign : VK_QUEUE_FAMILY_IGNORED;
	uint32_t to = use_foreign ? vk->family : VK_QUEUE_FAMILY_IGNORED;
	// Acquire the display buffer like wlroots (first use: PREINITIALIZED)
	if (dst.image) {
		image_barrier(cb, dst.image, VK_IMAGE_LAYOUT_PREINITIALIZED, VK_IMAGE_LAYOUT_GENERAL,
			from, to, 0, VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_TRANSFER_WRITE_BIT);
	} else {
		buffer_barrier(cb, dst.buffer, from, to, 0, VK_ACCESS_TRANSFER_WRITE_BIT);
	}

	VkClearColorValue white = { .float32 = { 1, 1, 1, 1 } };
	VkImageSubresourceRange range = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
	switch (m) {
	case M_DRAW:
	case M_DRAW_NOFOREIGN:
	case M_CLEAR_PASS:
	case M_LINEAR_DRAW:
		create_view(vk, format, &dst);
		create_pass(vk, format, m == M_CLEAR_PASS, use_pattern, w, h, &pass);
		fb = draw(vk, cb, &pass, &dst, w, h, m == M_CLEAR_PASS);
		break;
	case M_CLEAR_CMD:
		vkCmdClearColorImage(cb, dst.image, VK_IMAGE_LAYOUT_GENERAL, &white, 1, &range);
		break;
	case M_DRAW_COPY:
	case M_DRAW_BLIT:
	case M_TO_BUFFER:
		create_optimal(vk, format, w, h, &src);
		create_view(vk, format, &src);
		create_pass(vk, format, false, use_pattern, w, h, &pass);
		image_barrier(cb, src.image, VK_IMAGE_LAYOUT_UNDEFINED, VK_IMAGE_LAYOUT_GENERAL,
			VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED, 0,
			VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT);
		fb = draw(vk, cb, &pass, &src, w, h, false);
		image_barrier(cb, src.image, VK_IMAGE_LAYOUT_GENERAL, VK_IMAGE_LAYOUT_GENERAL,
			VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED,
			VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT, VK_ACCESS_TRANSFER_READ_BIT);
		if (m == M_DRAW_COPY) {
			VkImageCopy region = {
				.srcSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
				.dstSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
				.extent = { w, h, 1 },
			};
			vkCmdCopyImage(cb, src.image, VK_IMAGE_LAYOUT_GENERAL,
				dst.image, VK_IMAGE_LAYOUT_GENERAL, 1, &region);
		} else if (m == M_DRAW_BLIT) {
			VkImageBlit region = {
				.srcSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
				.srcOffsets = { { 0, 0, 0 }, { (int32_t)w, (int32_t)h, 1 } },
				.dstSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
				.dstOffsets = { { 0, 0, 0 }, { (int32_t)w, (int32_t)h, 1 } },
			};
			vkCmdBlitImage(cb, src.image, VK_IMAGE_LAYOUT_GENERAL,
				dst.image, VK_IMAGE_LAYOUT_GENERAL, 1, &region, VK_FILTER_NEAREST);
		} else {
			VkBufferImageCopy region = {
				.bufferOffset = s->offset,
				.bufferRowLength = s->stride / 4,
				.imageSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
				.imageExtent = { w, h, 1 },
			};
			vkCmdCopyImageToBuffer(cb, src.image, VK_IMAGE_LAYOUT_GENERAL,
				dst.buffer, 1, &region);
		}
		break;
	case M_BUFFER_COPY: {
		VkBufferCreateInfo bci = {
			.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
			.size = (VkDeviceSize)w * h * 4,
			.usage = VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
		};
		check(vkCreateBuffer(vk->dev, &bci, NULL, &host_buf), "vkCreateBuffer");
		VkMemoryRequirements req;
		vkGetBufferMemoryRequirements(vk->dev, host_buf, &req);
		int type = find_mem_type(vk, req.memoryTypeBits,
			VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
		VkMemoryAllocateInfo mai = {
			.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
			.allocationSize = req.size,
			.memoryTypeIndex = (uint32_t)(type < 0 ? 0 : type),
		};
		check(vkAllocateMemory(vk->dev, &mai, NULL, &host_mem), "vkAllocateMemory (host)");
		check(vkBindBufferMemory(vk->dev, host_buf, host_mem, 0), "vkBindBufferMemory");
		uint32_t *p;
		check(vkMapMemory(vk->dev, host_mem, 0, bci.size, 0, (void **)&p), "vkMapMemory");
		for (uint32_t y = 0; y < h; y++) {
			for (uint32_t x = 0; x < w; x++) {
				p[(size_t)y * w + x] = pattern(x, y);
			}
		}
		vkUnmapMemory(vk->dev, host_mem);
		VkBufferImageCopy region = {
			.imageSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
			.imageExtent = { w, h, 1 },
		};
		vkCmdCopyBufferToImage(cb, host_buf, dst.image, VK_IMAGE_LAYOUT_GENERAL, 1, &region);
		break;
	}
	default:
		break;
	}

	// Release to the display (FOREIGN), as wlroots does after rendering
	if (dst.image) {
		image_barrier(cb, dst.image, VK_IMAGE_LAYOUT_GENERAL, VK_IMAGE_LAYOUT_GENERAL, to, from,
			VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_TRANSFER_WRITE_BIT, 0);
	} else {
		buffer_barrier(cb, dst.buffer, to, from, VK_ACCESS_TRANSFER_WRITE_BIT, 0);
	}
	check(vkEndCommandBuffer(cb), "vkEndCommandBuffer");

	VkSubmitInfo si = {
		.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
		.commandBufferCount = 1,
		.pCommandBuffers = &cb,
	};
	check(vkQueueSubmit(vk->queue, 1, &si, VK_NULL_HANDLE), "vkQueueSubmit");
	check(vkQueueWaitIdle(vk->queue), "vkQueueWaitIdle");

	result = scanout_check(s, use_pattern, msg, msg_size);
	if (verbose && result != OUT_OK && use_pattern) {
		scanout_dump(s);
	}

	vkFreeCommandBuffers(vk->dev, vk->pool, 1, &cb);
	if (fb) {
		vkDestroyFramebuffer(vk->dev, fb, NULL);
	}
	if (pass.pipeline) {
		destroy_pass(vk, &pass);
	}
	destroy_target(vk, &src);
	destroy_target(vk, &dst);
	if (host_buf) {
		vkDestroyBuffer(vk->dev, host_buf, NULL);
		vkFreeMemory(vk->dev, host_mem, NULL);
	}
	return result;
}

static int open_kms_card(const char *path) {
	if (path) {
		int fd = open(path, O_RDWR | O_CLOEXEC);
		if (fd >= 0) {
			drmModeResPtr res = drmModeGetResources(fd);
			if (!res || res->count_connectors == 0) {
				fprintf(stderr, "vk-scanout-test: %s has no display connectors; "
					"use the display device (e.g. /dev/dri/card1)\n", path);
				drmModeFreeResources(res);
				close(fd);
				return -1;
			}
			drmModeFreeResources(res);
			info("Display device: %s\n", path);
		}
		return fd;
	}
	drmDevicePtr devices[16];
	int n = drmGetDevices2(0, devices, 16);
	int fd = -1;
	for (int i = 0; i < n && fd < 0; i++) {
		if (!(devices[i]->available_nodes & (1 << DRM_NODE_PRIMARY))) {
			continue;
		}
		int f = open(devices[i]->nodes[DRM_NODE_PRIMARY], O_RDWR | O_CLOEXEC);
		if (f < 0) {
			continue;
		}
		drmModeResPtr res = drmModeGetResources(f);
		if (res && res->count_connectors > 0) {
			info("Display device: %s\n", devices[i]->nodes[DRM_NODE_PRIMARY]);
			fd = f;
		} else {
			close(f);
		}
		drmModeFreeResources(res);
	}
	drmFreeDevices(devices, n);
	return fd;
}

int main(int argc, char **argv) {
	const char *dev_path = NULL;
	uint32_t w = 1024, h = 600;
	int opt;
	while ((opt = getopt(argc, argv, "d:s:qvh")) != -1) {
		switch (opt) {
		case 'd': dev_path = optarg; break;
		case 's':
			if (sscanf(optarg, "%ux%u", &w, &h) != 2) {
				fprintf(stderr, "invalid size %s\n", optarg);
				return 2;
			}
			break;
		case 'q': quiet = true; break;
		case 'v': verbose = true; break;
		default:
			fprintf(stderr, "usage: %s [-d /dev/dri/cardN] [-s WxH] [-q] [-v]\n", argv[0]);
			return opt == 'h' ? 0 : 2;
		}
	}

	int kms_fd = open_kms_card(dev_path);
	if (kms_fd < 0) {
		fprintf(stderr, "vk-scanout-test: no display device found (try -d)\n");
		return 2;
	}
	struct gbm_device *gbm = gbm_create_device(kms_fd);
	if (!gbm) {
		fprintf(stderr, "vk-scanout-test: gbm_create_device failed\n");
		return 2;
	}

	struct vk vk = {0};
	vk_init(&vk);

	struct {
		uint32_t drm;
		VkFormat vk;
		bool unorm;
		const char *name;
	} formats[] = {
		// wlroots maps XRGB8888 (the desktop's buffers) to B8G8R8A8_SRGB
		{ DRM_FORMAT_XRGB8888, VK_FORMAT_B8G8R8A8_SRGB, false, "XRGB8888 as B8G8R8A8_SRGB" },
		// UNORM: exact values, so the position pattern is used
		{ DRM_FORMAT_ARGB8888, VK_FORMAT_B8G8R8A8_UNORM, true, "ARGB8888 as B8G8R8A8_UNORM" },
	};
	const char *kind_names[] = {
		[DUMB] = "dumb buffer (what the display scans out)",
		[GBM] = "GBM buffer, as wlroots allocates (readback may be stale)",
	};

	// Results on the dumb buffers decide: their readback is reliable
	bool ok[M_COUNT];
	bool tested[M_COUNT] = {0};
	for (int m = 0; m < M_COUNT; m++) {
		ok[m] = true;
	}
	for (size_t f = 0; f < sizeof(formats) / sizeof(formats[0]); f++) {
		info("\n");
		print_format(&vk, formats[f].vk, formats[f].name);
		for (int k = DUMB; k <= GBM; k++) {
			struct scanout s;
			if (!scanout_create(k, kms_fd, gbm, formats[f].drm, w, h, &s)) {
				scanout_destroy(&s);
				continue;
			}
			char *mname = drmGetFormatModifierName(s.modifier);
			info(" %s, %ux%u, stride %u, modifier %s:\n", kind_names[k], w, h,
				s.stride, mname ? mname : "INVALID (implicit)");
			free(mname);
			for (int m = 0; m < M_COUNT; m++) {
				char msg[160];
				enum outcome r = run_method(&vk, &s, formats[f].vk, formats[f].unorm,
					m, msg, sizeof(msg));
				info("  %-46s %s\n", method_names[m], msg);
				if (k == DUMB) {
					tested[m] = true;
					ok[m] = ok[m] && r == OUT_OK;
				}
			}
			scanout_destroy(&s);
		}
	}

	const char *mode = "none";
	if (tested[M_DRAW] && ok[M_DRAW]) {
		mode = "direct";
	} else if (tested[M_DRAW_COPY] && ok[M_DRAW_COPY]) {
		mode = "copy";
	} else if (tested[M_TO_BUFFER] && ok[M_TO_BUFFER]) {
		mode = "buffer";
	}
	if (quiet) {
		printf("%s\n", mode);
	} else {
		printf("\nRecommended wlroots Vulkan mode: %s\n", mode);
		if (strcmp(mode, "copy") == 0) {
			printf("  -> desktop strategy vulkan-copy (WLR_VK_RENDER_COPY=1)\n");
		} else if (strcmp(mode, "buffer") == 0) {
			printf("  -> images in display memory get the wrong layout; desktop strategy "
				"vulkan-buffer (WLR_VK_RENDER_BUFFER=1)\n");
		} else if (strcmp(mode, "none") == 0) {
			printf("  -> Vulkan can't write display buffers correctly on this driver; use gles2\n");
		}
	}

	vkDestroyCommandPool(vk.dev, vk.pool, NULL);
	vkDestroyDevice(vk.dev, NULL);
	vkDestroyInstance(vk.instance, NULL);
	gbm_device_destroy(gbm);
	close(kms_fd);
	return strcmp(mode, "none") == 0 ? 1 : 0;
}
