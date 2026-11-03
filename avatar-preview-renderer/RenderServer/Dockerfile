# Native render server: the Linux player from Editor.RenderServerBuild.Build, rendering on the CPU
# through Mesa's llvmpipe OpenGL driver inside a virtual X display. No GPU is needed.
#
# Build (from avatar-preview-renderer/, after the player is built to Builds/RenderServer/):
#   docker build --platform linux/amd64 -f RenderServer/Dockerfile -t avatar-preview-render-server .
FROM ubuntu:24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        libgl1 \
        libglx-mesa0 \
        libgl1-mesa-dri \
        xvfb \
        libx11-6 libxcursor1 libxext6 libxi6 libxinerama1 libxrandr2 libxxf86vm1 \
        libglib2.0-0t64 \
    && rm -rf /var/lib/apt/lists/*

# The player lists Vulkan first; with no Vulkan driver installed it falls back to OpenGL. The player
# rejects Mesa's CPU Vulkan device (lavapipe), so installing it would not help.
ENV LIBGL_ALWAYS_SOFTWARE=1
ENV GALLIUM_DRIVER=llvmpipe
# Rasteriser threads. Match the container's CPU limit.
ENV LP_NUM_THREADS=4
# llvmpipe compiles each shader variant on first use; a volume here keeps them across restarts.
ENV MESA_SHADER_CACHE_DIR=/var/cache/mesa

COPY Builds/RenderServer/ /opt/renderer/
COPY RenderServer/entrypoint.sh /opt/renderer/entrypoint.sh
RUN chmod +x /opt/renderer/entrypoint.sh /opt/renderer/renderer.x86_64 \
    && mkdir -p /var/cache/mesa /data/out

WORKDIR /opt/renderer
ENTRYPOINT ["/opt/renderer/entrypoint.sh"]
CMD ["--serve", "--out", "/data/out"]
