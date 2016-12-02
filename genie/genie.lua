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

solution "multiplayerServer"
  configurations { "Release", "Debug" }
  location (_OPTIONS["to"])
  
  configuration "Debug"
    defines { "_DEBUG", "LUA_COMPAT_MODULE" }
    flags { "Symbols" }
    debugdir "../project/build/bin/debug"
    os.mkdir "../project/build/bin/debug"

  configuration "Release"
    defines { "NDEBUG", "LUA_COMPAT_MODULE" }
    flags { "Optimize" }
    debugdir "../project/build/bin/release"
    os.mkdir "../project/build/bin/release"

  configuration "vs*"
    defines { "_CRT_SECURE_NO_WARNINGS" }

  configuration "windows"
    targetdir "../project/build/bin/windows"

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
------------- MULTIPLAYER SERVER ---------------
--]]--------------------------------------------
project "multiplayerServer"
  targetname "multiplayerServer"
  language "C#"
  location "../project/build"
  libdirs "../lib"
  kind "ConsoleApp"
  
  includedirs {
    "../src",
  }
  
  files {
    "../src/**.*",
  }