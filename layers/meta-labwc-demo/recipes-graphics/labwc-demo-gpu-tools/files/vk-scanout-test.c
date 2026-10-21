// vk-scanout-test: check which Vulkan operations really write to a display
// (scanout) buffer on this GPU driver.
//
// A linear GBM buffer object, allocated the way the wlroots DRM backend
// allocates its scanout buffers, is filled with a sentinel value, imported
// into Vulkan as a DMA-BUF image (like wlroots' Vulkan renderer does), and
// then written with white using different Vulkan operations. After each one
// the buffer's memory is read back through the DMA-BUF mapping, i.e. the way
// the display controller sees it.
//
// Usage: vk-scanout-test [-d /dev/dri/cardN] [-s WIDTHxHEIGHT] [-q]
//   -q  print only the recommended wlroots Vulkan mode:
//       "direct" (renders into the buffer), "copy" (WLR_VK_RENDER_COPY=1),
//       or "none"
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

#define SENTINEL 0x11223344u

static bool quiet;
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

struct bo {
	struct gbm_bo *gbm;
	int fd;
	uint32_t width, height, stride, offset;
	uint64_t modifier;
	uint32_t *map;
	size_t map_size;
};

struct target {
	VkImage image;
	VkDeviceMemory memory;
	VkImageView view;
};

enum method {
	M_DRAW,          // wlroots: draw into the DMA-BUF image, FOREIGN barriers
	M_DRAW_NOFOREIGN,// same without queue family ownership transfers
	M_CLEAR_PASS,    // render pass with loadOp CLEAR
	M_CLEAR_CMD,     // vkCmdClearColorImage
	M_DRAW_COPY,     // draw into an optimal image, vkCmdCopyImage (WLR_VK_RENDER_COPY)
	M_DRAW_BLIT,     // draw into an optimal image, vkCmdBlitImage
	M_BUFFER_COPY,   // vkCmdCopyBufferToImage from a host buffer
	M_COUNT,
};

static const char *method_names[M_COUNT] = {
	"draw (wlroots default)",
	"draw, no FOREIGN barrier",
	"render pass clear",
	"vkCmdClearColorImage",
	"draw + copy (WLR_VK_RENDER_COPY)",
	"draw + blit",
	"buffer copy",
};

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

static void print_modifiers(struct vk *vk, VkFormat format, const char *name) {
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
	info("%s DRM format modifiers:\n", name);
	for (uint32_t i = 0; i < list.drmFormatModifierCount; i++) {
		VkFormatFeatureFlags f = mods[i].drmFormatModifierTilingFeatures;
		char *mname = drmGetFormatModifierName(mods[i].drmFormatModifier);
		info("  %-28s color-attachment=%d blend=%d blit-dst=%d transfer-dst=%d sampled=%d\n",
			mname ? mname : "?",
			!!(f & VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BIT),
			!!(f & VK_FORMAT_FEATURE_COLOR_ATTACHMENT_BLEND_BIT),
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

static bool bo_create(struct gbm_device *gbm, uint32_t format, uint32_t w, uint32_t h,
		struct bo *bo) {
	memset(bo, 0, sizeof(*bo));
	uint64_t linear = DRM_FORMAT_MOD_LINEAR;
	bo->gbm = gbm_bo_create_with_modifiers(gbm, w, h, format, &linear, 1);
	if (bo->gbm == NULL) {
		bo->gbm = gbm_bo_create(gbm, w, h, format,
			GBM_BO_USE_SCANOUT | GBM_BO_USE_RENDERING | GBM_BO_USE_LINEAR);
	}
	if (bo->gbm == NULL) {
		fprintf(stderr, "vk-scanout-test: gbm_bo_create failed: %s\n", strerror(errno));
		return false;
	}
	bo->width = w;
	bo->height = h;
	bo->fd = gbm_bo_get_fd(bo->gbm);
	bo->stride = gbm_bo_get_stride_for_plane(bo->gbm, 0);
	bo->offset = gbm_bo_get_offset(bo->gbm, 0);
	bo->modifier = gbm_bo_get_modifier(bo->gbm);
	bo->map_size = (size_t)bo->offset + (size_t)bo->stride * h;
	bo->map = mmap(NULL, bo->map_size, PROT_READ | PROT_WRITE, MAP_SHARED, bo->fd, 0);
	if (bo->map == MAP_FAILED) {
		fprintf(stderr, "vk-scanout-test: mmap of the DMA-BUF failed: %s\n", strerror(errno));
		return false;
	}
	return true;
}

static void bo_sync(struct bo *bo, uint64_t flags) {
	struct dma_buf_sync sync = { .flags = flags };
	ioctl(bo->fd, DMA_BUF_IOCTL_SYNC, &sync);
}

static void bo_fill(struct bo *bo, uint32_t value) {
	bo_sync(bo, DMA_BUF_SYNC_START | DMA_BUF_SYNC_WRITE);
	for (uint32_t y = 0; y < bo->height; y++) {
		uint32_t *row = (uint32_t *)((char *)bo->map + bo->offset + (size_t)y * bo->stride);
		for (uint32_t x = 0; x < bo->width; x++) {
			row[x] = value;
		}
	}
	bo_sync(bo, DMA_BUF_SYNC_END | DMA_BUF_SYNC_WRITE);
}

// Returns the share of white pixels (RGB = 0xffffff), and the share of
// pixels that still hold the sentinel
static void bo_check(struct bo *bo, double *white, double *untouched, uint32_t *other) {
	size_t n_white = 0, n_sentinel = 0, total = (size_t)bo->width * bo->height;
	*other = 0;
	bo_sync(bo, DMA_BUF_SYNC_START | DMA_BUF_SYNC_READ);
	for (uint32_t y = 0; y < bo->height; y++) {
		const uint32_t *row = (const uint32_t *)((const char *)bo->map + bo->offset +
			(size_t)y * bo->stride);
		for (uint32_t x = 0; x < bo->width; x++) {
			if ((row[x] & 0x00ffffffu) == 0x00ffffffu) {
				n_white++;
			} else if (row[x] == SENTINEL) {
				n_sentinel++;
			} else if (*other == 0) {
				*other = row[x];
			}
		}
	}
	bo_sync(bo, DMA_BUF_SYNC_END | DMA_BUF_SYNC_READ);
	*white = (double)n_white / total;
	*untouched = (double)n_sentinel / total;
}

static bool import_bo(struct vk *vk, struct bo *bo, VkFormat format, struct target *t) {
	VkExternalMemoryImageCreateInfo emi = {
		.sType = VK_STRUCTURE_TYPE_EXTERNAL_MEMORY_IMAGE_CREATE_INFO,
		.handleTypes = VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT,
	};
	VkSubresourceLayout layout = {
		.offset = bo->offset,
		.rowPitch = bo->stride,
	};
	uint64_t modifier = bo->modifier == DRM_FORMAT_MOD_INVALID ?
		DRM_FORMAT_MOD_LINEAR : bo->modifier;
	VkImageDrmFormatModifierExplicitCreateInfoEXT mod = {
		.sType = VK_STRUCTURE_TYPE_IMAGE_DRM_FORMAT_MODIFIER_EXPLICIT_CREATE_INFO_EXT,
		.drmFormatModifier = modifier,
		.drmFormatModifierPlaneCount = 1,
		.pPlaneLayouts = &layout,
	};
	emi.pNext = &mod;
	VkImageCreateInfo ici = {
		.sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO,
		.pNext = &emi,
		.imageType = VK_IMAGE_TYPE_2D,
		.format = format,
		.extent = { bo->width, bo->height, 1 },
		.mipLevels = 1,
		.arrayLayers = 1,
		.samples = VK_SAMPLE_COUNT_1_BIT,
		.tiling = VK_IMAGE_TILING_DRM_FORMAT_MODIFIER_EXT,
		.usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_SRC_BIT |
			VK_IMAGE_USAGE_TRANSFER_DST_BIT,
		.sharingMode = VK_SHARING_MODE_EXCLUSIVE,
		.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED,
	};
	VkResult res = vkCreateImage(vk->dev, &ici, NULL, &t->image);
	if (res != VK_SUCCESS) {
		info("  vkCreateImage (DMA-BUF import) failed: %d\n", res);
		return false;
	}

	VkMemoryFdPropertiesKHR fdp = { .sType = VK_STRUCTURE_TYPE_MEMORY_FD_PROPERTIES_KHR };
	check(vk->get_memory_fd_properties(vk->dev,
		VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT, bo->fd, &fdp),
		"vkGetMemoryFdPropertiesKHR");
	VkMemoryRequirements req;
	vkGetImageMemoryRequirements(vk->dev, t->image, &req);
	int type = find_mem_type(vk, req.memoryTypeBits & fdp.memoryTypeBits, 0);
	if (type < 0) {
		info("  no memory type for the DMA-BUF\n");
		return false;
	}
	VkMemoryDedicatedAllocateInfo dedicated = {
		.sType = VK_STRUCTURE_TYPE_MEMORY_DEDICATED_ALLOCATE_INFO,
		.image = t->image,
	};
	VkImportMemoryFdInfoKHR import = {
		.sType = VK_STRUCTURE_TYPE_IMPORT_MEMORY_FD_INFO_KHR,
		.pNext = &dedicated,
		.handleType = VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT,
		.fd = fcntl(bo->fd, F_DUPFD_CLOEXEC, 0),
	};
	VkMemoryAllocateInfo mai = {
		.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
		.pNext = &import,
		.allocationSize = req.size,
		.memoryTypeIndex = (uint32_t)type,
	};
	check(vkAllocateMemory(vk->dev, &mai, NULL, &t->memory), "vkAllocateMemory (import)");
	check(vkBindImageMemory(vk->dev, t->image, t->memory, 0), "vkBindImageMemory");
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
	if (t->memory) {
		vkFreeMemory(vk->dev, t->memory, NULL);
	}
	memset(t, 0, sizeof(*t));
}

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

static void create_pass(struct vk *vk, VkFormat format, bool clear, uint32_t w, uint32_t h,
		struct pass *p) {
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
	VkShaderModule fs = shader(vk, fill_frag, sizeof(fill_frag));
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

static void barrier(VkCommandBuffer cb, VkImage image, VkImageLayout from, VkImageLayout to,
		uint32_t src_family, uint32_t dst_family, VkAccessFlags src_access,
		VkAccessFlags dst_access) {
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

static const char *run_method(struct vk *vk, struct bo *bo, VkFormat format, enum method m,
		double *white_out) {
	uint32_t w = bo->width, h = bo->height;
	uint32_t foreign = vk->has_foreign ? VK_QUEUE_FAMILY_FOREIGN_EXT : VK_QUEUE_FAMILY_IGNORED;
	struct target dst = {0}, src = {0};
	struct pass pass = {0};
	VkFramebuffer fb = VK_NULL_HANDLE;
	VkBuffer host_buf = VK_NULL_HANDLE;
	VkDeviceMemory host_mem = VK_NULL_HANDLE;
	const char *result;

	bo_fill(bo, SENTINEL);
	if (!import_bo(vk, bo, format, &dst)) {
		*white_out = 0;
		return "import failed";
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
	// Acquire the DMA-BUF image like wlroots (first use: PREINITIALIZED)
	barrier(cb, dst.image, VK_IMAGE_LAYOUT_PREINITIALIZED, VK_IMAGE_LAYOUT_GENERAL,
		from, to, 0, VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_TRANSFER_WRITE_BIT);

	VkClearColorValue white = { .float32 = { 1, 1, 1, 1 } };
	VkImageSubresourceRange range = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
	switch (m) {
	case M_DRAW:
	case M_DRAW_NOFOREIGN:
	case M_CLEAR_PASS:
		create_view(vk, format, &dst);
		create_pass(vk, format, m == M_CLEAR_PASS, w, h, &pass);
		fb = draw(vk, cb, &pass, &dst, w, h, m == M_CLEAR_PASS);
		break;
	case M_CLEAR_CMD:
		vkCmdClearColorImage(cb, dst.image, VK_IMAGE_LAYOUT_GENERAL, &white, 1, &range);
		break;
	case M_DRAW_COPY:
	case M_DRAW_BLIT:
		create_optimal(vk, format, w, h, &src);
		create_view(vk, format, &src);
		create_pass(vk, format, false, w, h, &pass);
		barrier(cb, src.image, VK_IMAGE_LAYOUT_UNDEFINED, VK_IMAGE_LAYOUT_GENERAL,
			VK_QUEUE_FAMILY_IGNORED, VK_QUEUE_FAMILY_IGNORED, 0,
			VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT);
		fb = draw(vk, cb, &pass, &src, w, h, false);
		barrier(cb, src.image, VK_IMAGE_LAYOUT_GENERAL, VK_IMAGE_LAYOUT_GENERAL,
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
		} else {
			VkImageBlit region = {
				.srcSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
				.srcOffsets = { { 0, 0, 0 }, { (int32_t)w, (int32_t)h, 1 } },
				.dstSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 },
				.dstOffsets = { { 0, 0, 0 }, { (int32_t)w, (int32_t)h, 1 } },
			};
			vkCmdBlitImage(cb, src.image, VK_IMAGE_LAYOUT_GENERAL,
				dst.image, VK_IMAGE_LAYOUT_GENERAL, 1, &region, VK_FILTER_NEAREST);
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
		void *p;
		check(vkMapMemory(vk->dev, host_mem, 0, bci.size, 0, &p), "vkMapMemory");
		memset(p, 0xff, bci.size);
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
	barrier(cb, dst.image, VK_IMAGE_LAYOUT_GENERAL, VK_IMAGE_LAYOUT_GENERAL, to, from,
		VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_TRANSFER_WRITE_BIT, 0);
	check(vkEndCommandBuffer(cb), "vkEndCommandBuffer");

	VkSubmitInfo si = {
		.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
		.commandBufferCount = 1,
		.pCommandBuffers = &cb,
	};
	check(vkQueueSubmit(vk->queue, 1, &si, VK_NULL_HANDLE), "vkQueueSubmit");
	check(vkQueueWaitIdle(vk->queue), "vkQueueWaitIdle");

	double white_share, untouched;
	uint32_t other;
	bo_check(bo, &white_share, &untouched, &other);
	*white_out = white_share;
	static char buf[96];
	if (white_share > 0.999) {
		result = "OK (white)";
	} else if (untouched > 0.999) {
		result = "NOT WRITTEN (memory unchanged)";
	} else {
		snprintf(buf, sizeof(buf), "WRONG (%.0f%% white, %.0f%% unchanged, e.g. 0x%08x)",
			white_share * 100, untouched * 100, other);
		result = buf;
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
		return open(path, O_RDWR | O_CLOEXEC);
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
	while ((opt = getopt(argc, argv, "d:s:qh")) != -1) {
		switch (opt) {
		case 'd': dev_path = optarg; break;
		case 's':
			if (sscanf(optarg, "%ux%u", &w, &h) != 2) {
				fprintf(stderr, "invalid size %s\n", optarg);
				return 2;
			}
			break;
		case 'q': quiet = true; break;
		default:
			fprintf(stderr, "usage: %s [-d /dev/dri/cardN] [-s WxH] [-q]\n", argv[0]);
			return opt == 'h' ? 0 : 2;
		}
	}

	int fd = open_kms_card(dev_path);
	if (fd < 0) {
		fprintf(stderr, "vk-scanout-test: no display device found (try -d)\n");
		return 2;
	}
	struct gbm_device *gbm = gbm_create_device(fd);
	if (!gbm) {
		fprintf(stderr, "vk-scanout-test: gbm_create_device failed\n");
		return 2;
	}

	struct vk vk = {0};
	vk_init(&vk);

	struct {
		uint32_t drm;
		VkFormat vk;
		const char *name;
	} formats[] = {
		// wlroots maps XRGB8888 (the format of the desktop's buffers) to
		// B8G8R8A8_SRGB and ARGB8888 to B8G8R8A8_UNORM
		{ DRM_FORMAT_XRGB8888, VK_FORMAT_B8G8R8A8_SRGB, "XRGB8888 as B8G8R8A8_SRGB" },
		{ DRM_FORMAT_ARGB8888, VK_FORMAT_B8G8R8A8_UNORM, "ARGB8888 as B8G8R8A8_UNORM" },
	};

	bool direct_ok = true, copy_ok = true;
	for (size_t f = 0; f < sizeof(formats) / sizeof(formats[0]); f++) {
		info("\n");
		print_modifiers(&vk, formats[f].vk, formats[f].name);
		struct bo bo;
		if (!bo_create(gbm, formats[f].drm, w, h, &bo)) {
			return 2;
		}
		char *mname = drmGetFormatModifierName(bo.modifier);
		info("GBM buffer %ux%u: stride %u, offset %u, modifier %s\n", w, h, bo.stride,
			bo.offset, mname ? mname : "INVALID (implicit)");
		free(mname);
		for (int m = 0; m < M_COUNT; m++) {
			double white;
			const char *r = run_method(&vk, &bo, formats[f].vk, m, &white);
			info("  %-34s %s\n", method_names[m], r);
			bool ok = white > 0.999;
			if (m == M_DRAW && !ok) {
				direct_ok = false;
			}
			if (m == M_DRAW_COPY && !ok) {
				copy_ok = false;
			}
		}
		munmap(bo.map, bo.map_size);
		close(bo.fd);
		gbm_bo_destroy(bo.gbm);
	}

	const char *mode = direct_ok ? "direct" : copy_ok ? "copy" : "none";
	if (quiet) {
		printf("%s\n", mode);
	} else {
		printf("\nRecommended wlroots Vulkan mode: %s\n", mode);
		if (!direct_ok && copy_ok) {
			printf("  -> rendering into display buffers doesn't reach memory; use "
				"LABWC_RENDERERS=vulkan-copy (WLR_VK_RENDER_COPY=1)\n");
		} else if (!direct_ok) {
			printf("  -> Vulkan can't write display buffers on this driver; use gles2\n");
		}
	}

	vkDestroyCommandPool(vk.dev, vk.pool, NULL);
	vkDestroyDevice(vk.dev, NULL);
	vkDestroyInstance(vk.instance, NULL);
	gbm_device_destroy(gbm);
	close(fd);
	return strcmp(mode, "none") == 0 ? 1 : 0;
}
