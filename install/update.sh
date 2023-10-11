#!/bin/bash

if [[ $(getconf LONG_BIT) =~ "32" ]]
then
    file="HTFanControl_RasPi.zip"
elif [[ $(uname -m) =~ "aarch" ]]
then
    file="HTFanControl_RasPi64.zip"
else
    file="HTFanControl_Linux.zip"
fi

wget -O /tmp/$file https://github.com/nicko88/HTFanControl/releases/latest/download/$file
rm /opt/HTFanControl/HTFanControl
unzip /tmp/$file -d /opt/HTFanControl
chmod +x /opt/HTFanControl/HTFanControl
rm /tmp/$file
rm /opt/HTFanControl/update.sh
rm update.sh
service HTFanControl restart