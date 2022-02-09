#!/bin/bash

case $(uname -m) in
	*"arm"*)
	file="HTFanControl_RasPi.zip"
	;;
	
	*"aarch"*)
	file="HTFanControl_RasPi64.zip"
	;;
	
	*"x86_64"*)
	file="HTFanControl_Linux.zip"
	;;
esac

wget -O /tmp/$file https://github.com/nicko88/HTFanControl/releases/latest/download/$file
rm /opt/HTFanControl/HTFanControl
unzip /tmp/$file -d /opt/HTFanControl
chmod +x /opt/HTFanControl/HTFanControl
rm /tmp/$file
rm /opt/HTFanControl/update.sh
rm update.sh
service HTFanControl restart