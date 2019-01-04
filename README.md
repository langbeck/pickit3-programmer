# PICkit3 Programmer Application for Linux

This is the original [Microchip's PICkit3 Programmer Application][1] that has
been ported to Linux. Most of the code stills the same, some changes include:
- Replace native calls to Windows USB API for the [cross-platform HidSharp][2];
- Fix Windows-like file paths and manual path build;

[1]: http://ww1.microchip.com/downloads/en/DeviceDoc/PICkit3%20Programmer%20Application%20v3.10.zip
[2]: https://www.nuget.org/packages/HidSharp/

## 1. Pre-requisites
- for building: `sudo apt install -y mono-complete`
- for execute: `sudo apt install -y mono-runtime`

## 2. Building
Restore Nuget packages and build: `make build` or only `make`

## 3. Installing
Installation uses `/opt/microchip/pickit3` as the target directory and covers:
- Copy build output and extra files to the target directory;
- Copy udev rules to `/etc/udev/rules.d/`;
- Create a link to `pickit3` in `/usr/local/bin/pickit3`

To install just run: `sudo make install`
