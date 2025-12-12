## Makefile for protocol generation
## Must be executed from project root.
## Commands run inside ./scripts directory.

SCRIPTS_DIR := ./scripts

.PHONY: upgrade-protocol-by-url regenerate-protocol upgrade-protocol clean

## Generate C# code from protobuf definitions
regenerate-protocol:
	@echo "Installing npm dependencies..."
	cd $(SCRIPTS_DIR) && npm install

	@echo "Building protocol (generating C# files)..."
	cd $(SCRIPTS_DIR) && npm run build-protocol

	@echo "Protocol generation complete."


## Upgrade @dcl/protocol to experimental
## and regenerate the C# protobuf bindings
upgrade-protocol:
	@echo "Updating @dcl/protocol to experimental..."
	cd $(SCRIPTS_DIR) && npm install @dcl/protocol@experimental

	@echo "Running protocol generation after upgrade..."
	cd $(SCRIPTS_DIR) && npm run build-protocol

	@echo "Protocol upgraded and regenerated."

## Install @dcl/protocol from a GitHub PR tarball URL
## Usage:
## make protocol-url URL="https://example.com/dcl-protocol.tgz"
upgrade-protocol-by-url:
ifndef URL
	$(error URL is not set. Usage: make protocol-url URL=\"https://...tgz\")
endif
	@echo "Installing @dcl/protocol from URL:"
	@echo "$(URL)"
	cd $(SCRIPTS_DIR) && npm install "$(URL)"

	@echo "Running protocol generation..."
	cd $(SCRIPTS_DIR) && npm run build-protocol

	@echo "Protocol installed from PR URL and regenerated."

## Clean node_modules in scripts
clean:
	@echo "Removing node_modules..."
	rm -rf $(SCRIPTS_DIR)/node_modules
	@echo "Clean complete."

