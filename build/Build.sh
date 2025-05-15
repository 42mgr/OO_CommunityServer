#!/bin/sh
export MONO_IOMAP=all
VSToolsPath=msbuild
msbuild build/msbuild/build.proj /flp:LogFile=Build.log
