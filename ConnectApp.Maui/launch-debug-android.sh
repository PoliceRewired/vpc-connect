#!/bin/bash

rm -r ConnectApp.Maui/bin
rm -r ConnectApp.Maui/obj
adb uninstall org.vpc.connect
dotnet build -t:Run -f net7.0-android --configuration Debug
