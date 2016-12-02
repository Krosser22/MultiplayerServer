--
-- ////////////////////////////////////////////
-- /////Autor: Juan Daniel Laserna Condado/////
-- /////Email: S6106112@live.tees.ac.uk   /////
-- /////            2016-2017             /////
-- ////////////////////////////////////////////
--

-- Define the project. Put the release configuration first so it will be the
-- default when folks build using the makefile. That way they don't have to
-- worry about the /scripts argument and all that.
--

solution "multiplayer"
  configurations { "Release", "Debug" }
  location (_OPTIONS["to"])
  
  configuration "Debug"
    defines { "_DEBUG", "LUA_COMPAT_MODULE" }
    flags { "Symbols" }
    links {
      "openal32",
      "sfml-audio-d",
      "sfml-graphics-d",
      "sfml-network-d",
      "sfml-system-d",
      "sfml-window-d",
    }

  configuration "Release"
    defines { "NDEBUG", "LUA_COMPAT_MODULE" }
    flags { "Optimize" }
    links {
      "openal32",
      "sfml-graphics",
      "sfml-network",
      "sfml-system",
      "sfml-window",
    }

  configuration "vs*"
    defines { "_CRT_SECURE_NO_WARNINGS" }

  configuration "windows"
    os.mkdir("../projectClient/build/bin/windows")
    debugdir "../projectClient/build/bin/windows"
    targetdir "../projectClient/build/bin/windows"
    os.copyfile("../lib/openal32.lib", "../projectClient/build/bin/windows/openal32.lib")
    os.copyfile("../lib/openal32.dll", "../projectClient/build/bin/windows/openal32.dll")
    os.copyfile("../lib/sfml-audio-2.dll", "../projectClient/build/bin/windows/sfml-audio-2.dll")
    os.copyfile("../lib/sfml-audio.lib", "../projectClient/build/bin/windows/sfml-audio.lib")
    os.copyfile("../lib/sfml-audio-d-2.dll", "../projectClient/build/bin/windows/sfml-audio-d-2.dll")
    os.copyfile("../lib/sfml-audio-d.lib", "../projectClient/build/bin/windows/sfml-audio-d.lib")
    os.copyfile("../lib/sfml-graphics-2.dll", "../projectClient/build/bin/windows/sfml-graphics-2.dll")
    os.copyfile("../lib/sfml-graphics.lib", "../projectClient/build/bin/windows/sfml-graphics.lib")
    os.copyfile("../lib/sfml-graphics-d-2.dll", "../projectClient/build/bin/windows/sfml-graphics-d-2.dll")
    os.copyfile("../lib/sfml-graphics-d.lib", "../projectClient/build/bin/windows/sfml-graphics-d.lib")
    os.copyfile("../lib/sfml-network-2.dll", "../projectClient/build/bin/windows/sfml-network-2.dll")
    os.copyfile("../lib/sfml-network.lib", "../projectClient/build/bin/windows/sfml-network.lib")
    os.copyfile("../lib/sfml-network-d-2.dll", "../projectClient/build/bin/windows/sfml-network-d-2.dll")
    os.copyfile("../lib/sfml-network-d.lib", "../projectClient/build/bin/windows/sfml-network-d.lib")
    os.copyfile("../lib/sfml-system-2.dll", "../projectClient/build/bin/windows/sfml-system-2.dll")
    os.copyfile("../lib/sfml-system.lib", "../projectClient/build/bin/windows/sfml-system.lib")
    os.copyfile("../lib/sfml-system-d-2.dll", "../projectClient/build/bin/windows/sfml-system-d-2.dll")
    os.copyfile("../lib/sfml-system-d.lib", "../projectClient/build/bin/windows/sfml-system-d.lib")
    os.copyfile("../lib/sfml-window-2.dll", "../projectClient/build/bin/windows/sfml-window-2.dll")
    os.copyfile("../lib/sfml-window.lib", "../projectClient/build/bin/windows/sfml-window.lib")
    os.copyfile("../lib/sfml-window-d-2.dll", "../projectClient/build/bin/windows/sfml-window-d-2.dll")
    os.copyfile("../lib/sfml-window-d.lib", "../projectClient/build/bin/windows/sfml-window-d.lib")
--
-- Use the --to=path option to control where the project files get generated. I use
-- this to create project files for each supported toolset, each in their own folder,
-- in preparation for deployment.
--
  newoption {
    trigger = "to",
    value   = "path",
    description = "Set the output location for the generated files"
  }
  
--[[--------------------------------------------
------------- MULTIPLAYER CLIENT ---------------
--]]--------------------------------------------
project "multiplayerClient"
  targetname "multiplayerClient"
  language "C++"
  location "../projectClient/build"
  libdirs "../lib"
  kind "ConsoleApp"
  
  --[[flags {
    "No64BitChecks",
    "ExtraWarnings",
  }]]

  --defines {
    --"_GLFW_WIN32",
    --"_GLFW_WGL",
    --"_GLFW_USE_OPENGL",
  --}
  
  includedirs {
    --"../assets",
    "../include",
    "../deps/SFML/include",
  }
  
  files {
    "../src/**.*",
    "../include/**.*",
    "../deps/SFML/include/**.*",
  }