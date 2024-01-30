cmake_minimum_required(VERSION 3.11)
project(cimgui)

set(CMAKE_CXX_STANDARD 11)

if(WIN32) # to mingw work as all the others
	set(CMAKE_SHARED_LIBRARY_PREFIX "")
endif(WIN32)

include(FetchContent)
set(FETCHCONTENT_QUIET NO)

# defs
add_definitions("-DCIMGUI_USE_GLFW")
add_definitions("-DCIMGUI_USE_OPENGL3")

#add_definitions("-DIMGUI_IMPL_OPENGL_LOADER_GL3W")
add_definitions("-DIMGUI_DISABLE_OBSOLETE_FUNCTIONS=1")
if (WIN32)
    add_definitions("-DIMGUI_IMPL_API=extern \"C\" __declspec(dllexport)")
    list(APPEND IMGUI_LIBRARIES imm32)
else (WIN32)
    add_definitions("-DIMGUI_IMPL_API=extern \"C\" ")
endif (WIN32)

# files and includes
set(CIMGUI_DIR "${PROJECT_SOURCE_DIR}/../../modules/cimgui")
set(BACKENDS_DIR "${CIMGUI_DIR}/imgui/backends")

include_directories(${CIMGUI_DIR})
include_directories(${CIMGUI_DIR}/imgui)
include_directories(${CIMGUI_DIR}/imgui/backends)
include_directories(${CIMGUI_DIR}/imgui/examples/libs/glfw/include)

file(GLOB IMGUI_SOURCES
    ${CIMGUI_DIR}/cimgui.cpp
    ${CIMGUI_DIR}/imgui/imgui.cpp
    ${CIMGUI_DIR}/imgui/imgui_draw.cpp
    ${CIMGUI_DIR}/imgui/imgui_demo.cpp
    ${CIMGUI_DIR}/imgui/imgui_widgets.cpp
    ${CIMGUI_DIR}/imgui/imgui_tables.cpp
    ${BACKENDS_DIR}/imgui_impl_opengl3.cpp
    ${BACKENDS_DIR}/imgui_impl_glfw.cpp
)

set(IMGUI_LIBRARIES )

# opengl3
if(WIN32)
    list(APPEND IMGUI_LIBRARIES opengl32)
else(WIN32) # Unix
    list(APPEND IMGUI_LIBRARIES GL)
endif(WIN32)

# GLFW
set(GLFW_VERSION 3.2)
FetchContent_Declare(glfw URL https://github.com/glfw/glfw/archive/refs/tags/${GLFW_VERSION}.tar.gz)
FetchContent_GetProperties(glfw)
if (NOT glfw_POPULATED)
    FetchContent_Populate(glfw)
    set(BUILD_SHARED_LIBS ON CACHE BOOL "" FORCE)
    set(GLFW_BUILD_DOCS OFF CACHE BOOL "" FORCE)
    set(GLFW_BUILD_TESTS OFF CACHE BOOL "" FORCE)
    set(GLFW_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
    add_subdirectory(${glfw_SOURCE_DIR} ${glfw_BINARY_DIR})
endif()
include_directories(${glfw_SOURCE_DIR}/include)

# link with glfw
# add library and link
add_library(cimgui SHARED ${IMGUI_SOURCES})
#add_library(cimgui SHARED ${IMGUI_SOURCES_glfw} $<TARGET_OBJECTS:cimgui_base>)
# add_library(cimgui SHARED $<TARGET_OBJECTS:cimgui_base>)
add_definitions("-DGLFW_DLL")
target_link_libraries(cimgui
    PUBLIC ${IMGUI_LIBRRIES}
    PRIVATE glfw
)

#install
install(TARGETS cimgui
    RUNTIME DESTINATION "${CMAKE_BINARY_DIR}/cimgui"
    LIBRARY DESTINATION "${CMAKE_BINARY_DIR}/cimgui"
    ARCHIVE DESTINATION "${CMAKE_BINARY_DIR}/cimgui"
)
