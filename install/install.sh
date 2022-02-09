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

read -p "Are you sure you want to INSTALL HTFanControl? [y/n]" -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]
then
	mkdir /opt/HTFanControl
	wget -O /tmp/$file https://github.com/nicko88/HTFanControl/releases/latest/download/$file
	unzip /tmp/$file -d /opt/HTFanControl
	chmod +x /opt/HTFanControl/HTFanControl
	wget -O /lib/systemd/system/HTFanControl.service https://raw.githubusercontent.com/nicko88/HTFanControl/master/install/HTFanControl.service
	systemctl daemon-reload
	systemctl enable HTFanControl.service
	service HTFanControl start
	rm /tmp/$file
fi

read -p "Do you also want to INSTALL mosquitto MQTT broker? [y/n]" -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]
then
	apt-get update
	apt-get install -y mosquitto
	systemctl daemon-reload
	systemctl enable mosquitto.service
	echo "allow_anonymous true" >> /etc/mosquitto/mosquitto.conf
	echo "listener 1883" >> /etc/mosquitto/mosquitto.conf
	service mosquitto restart
fi

rm install.sh