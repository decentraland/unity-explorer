## Makefile for protocol generation and Unity test runs.
## Must be executed from project root.
## Commands run inside ./scripts directory.

SCRIPTS_DIR := ./scripts

## Unity test configuration. Override on the command line, e.g.:
##   make test-editmode TEST_FILTER=DCL.Tests.CodeConventionsTests
##   make test-playmode UNITY=/path/to/Unity
PROJECT_DIR      := ./Explorer
UNITY_VERSION    := $(shell grep -o 'm_EditorVersion: [0-9a-f.]*' $(PROJECT_DIR)/ProjectSettings/ProjectVersion.txt | cut -d' ' -f2)
UNITY            ?= /Applications/Unity/Hub/Editor/$(UNITY_VERSION)/Unity.app/Contents/MacOS/Unity
TEST_RESULTS_DIR ?= ./test-results
TEST_CATEGORY    ?= !Performance
TEST_FILTER      ?=

.PHONY: upgrade-protocol-by-url regenerate-protocol upgrade-protocol clean test-editmode test-playmode

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

## Run Unity EditMode tests in batch mode (matches CI flags).
test-editmode: TEST_MODE := editmode
test-editmode: _run-unity-tests

## Run Unity PlayMode tests in batch mode (matches CI flags).
test-playmode: TEST_MODE := playmode
test-playmode: _run-unity-tests

.PHONY: _run-unity-tests
_run-unity-tests:
	@mkdir -p $(TEST_RESULTS_DIR)
	@echo "Unity:         $(UNITY)"
	@echo "Project:       $(PROJECT_DIR)"
	@echo "Test mode:     $(TEST_MODE)"
	@echo "Test category: $(TEST_CATEGORY)"
	@echo "Test filter:   $(if $(TEST_FILTER),$(TEST_FILTER),(none))"
	@echo "Results:       $(TEST_RESULTS_DIR)/$(TEST_MODE).xml"
	"$(UNITY)" \
	  -batchmode \
	  -nographics \
	  -projectPath "$(PROJECT_DIR)" \
	  -runTests \
	  -testPlatform $(TEST_MODE) \
	  -testCategory "$(TEST_CATEGORY)" \
	  -burst-disable-compilation \
	  -accept-apiupdate \
	  $(if $(TEST_FILTER),-testFilter "$(TEST_FILTER)") \
	  -testResults "$(TEST_RESULTS_DIR)/$(TEST_MODE).xml" \
	  -logFile "$(TEST_RESULTS_DIR)/$(TEST_MODE).log"

