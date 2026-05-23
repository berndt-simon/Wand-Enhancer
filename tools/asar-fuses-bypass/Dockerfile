# Cross-compiles the version.dll proxy (x64 Windows PE) on Linux using MinGW-w64.
# The source is plain C11 + a .def export file; the CMakeLists already carries a
# GNU/Clang branch (-static -static-libgcc) so no MSVC toolchain is required.
#
# Build + extract the artifact (requires BuildKit, default in modern Docker):
#   docker build -o type=local,dest=./out tools/asar-fuses-bypass
# -> produces ./out/version.dll
#
# Pick a build type with --build-arg BUILD_TYPE=Debug (default Release).

FROM debian:bookworm-slim AS build
ARG BUILD_TYPE=Release
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        cmake make gcc-mingw-w64-x86-64 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY . .

RUN cmake -S . -B /build \
        -G "Unix Makefiles" \
        -DCMAKE_SYSTEM_NAME=Windows \
        -DCMAKE_C_COMPILER=x86_64-w64-mingw32-gcc \
        -DCMAKE_BUILD_TYPE="${BUILD_TYPE}" \
 && cmake --build /build

# Final stage holds only the artifact, so `docker build -o` exports just version.dll.
FROM scratch AS artifact
COPY --from=build /build/version.dll /version.dll
