
# HTFanControl
#### 4D Theater Wind Effect - DIY Home Theater Project

HTFanControl is an application meant to control fans in your home theater in order to create a wind effect during movies.

The program is meant to run in the background and be controlled through a web interface, typically from your smartphone.

### User Demo Video

[![User Demo Video](https://img.youtube.com/vi/iROCqS2yFdc/0.jpg)](https://www.youtube.com/watch?v=iROCqS2yFdc)

### Getting Started

There is a great project guide on the wiki [here](https://github.com/nicko88/HTFanControl/wiki/4D-Wind-Project-Guide-2021).

Otherwise come join the community forum thread to ask questions [here](https://www.avsforum.com/forum/28-tweaks-do-yourself/3152346-4d-theater-wind-effect-diy-home-theater-project.html).

You can find help from me (user: [SirMaster](https://www.avsforum.com/forum/members/8147918-sirmaster.html)) or other users of HTFanControl there.

### Raspberry Pi / Linux Installation
This install script is intended to install HTFanControl on RasPi or standard Linux running a Debian-based distribution using systemd.  It may work on other distributions but it has not been tested.  You can also download the Linux release and install it manually onto your particular Linux machine.

This script will ask to install HTFanControl and also additionally mosquitto MQTT broker which is needed to control the fan relay switch over the network.
#### Install
    sudo wget https://raw.githubusercontent.com/nicko88/HTFanControl/master/install/install.sh && sudo bash install.sh
#### Update
There is an update function built into the app at the bottom of the Settings screen, or you can run the update script manually here:

    sudo wget https://raw.githubusercontent.com/nicko88/HTFanControl/master/install/update.sh && sudo bash update.sh
#### Uninstall
    sudo wget https://raw.githubusercontent.com/nicko88/HTFanControl/master/install/uninstall.sh && sudo bash uninstall.sh

### Wind Tracks

HTFanControl uses specially created wind track files for each movie with coded time stamps and wind speeds.

A current database of wind track files created by the community is hosted [here](https://drive.google.com/drive/u/0/folders/13xoJMKeXX69woyt1Qzd_Qz_L6MUwTd1K).

These wind tracks can also be downloaded through the HTFanControl web interface as well.

#### Creating Wind Tracks

A companion app called WindTrackCreator has been created to help the process of making wind tracks for your movies.

You can find the WindTrackCreator project [here](https://github.com/nicko88/WindTrackCreator).