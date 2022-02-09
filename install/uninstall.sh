#!/bin/bash

read -p "Are you sure you want to UNINSTALL HTFanControl? [y/n]" -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]
then
	service HTFanControl stop
	systemctl disable HTFanControl.service
	rm /lib/systemd/system/HTFanControl.service
	systemctl daemon-reload
	rm -rf /opt/HTFanControl
fi

read -p "Do you also want to UNINSTALL mosquitto MQTT broker? [y/n]" -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]
then
	service mosquitto stop
	systemctl disable mosquitto.service
	rm /lib/systemd/system/mosquitto.service
	systemctl daemon-reload
	apt-get autoremove mosquitto --purge -y
fi

rm uninstall.sh