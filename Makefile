APP_NAME=PICkit3
PROJECT_DIR=PICkit2V2
BUILD_CONFIG=Release
INSTALL_DIR=/opt/microchip/pickit3

BUILD_DIR=$(PROJECT_DIR)/bin/$(BUILD_CONFIG)


build:
	nuget restore -NonInteractive
	msbuild -t:Build -p:Configuration="$(BUILD_CONFIG)" -v:quiet

install: install-bundle install-bin install-link install-udev-rules

install-udev-rules:
	cp 99-pickit.rules /etc/udev/rules.d

install-link:
	ln -sf "$(INSTALL_DIR)/bin/pickit3" /usr/local/bin/pickit3

install-bundle: $(INSTALL_DIR)
	cp -r --preserve=mode bundle/* "$(INSTALL_DIR)"

install-bin: $(INSTALL_DIR)
	chmod +x "$(BUILD_DIR)/$(APP_NAME).exe"
	cp -r --preserve=mode $(BUILD_DIR)/* "$(INSTALL_DIR)/bin"

$(INSTALL_DIR):
	mkdir -p "$(INSTALL_DIR)"

clean:
	rm -rf packages/
	rm -rf "$(PROJECT_DIR)/bin"
	rm -rf "$(PROJECT_DIR)/obj"
