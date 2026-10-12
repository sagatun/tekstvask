# Local builds are ad-hoc signed. `make release TEAM_ID=…` signs with Developer ID and notarizes.
PROJECT  = Tekstvask.xcodeproj
SCHEME   = Tekstvask
DERIVED  = build/DerivedData
APP      = build/Tekstvask.app
ZIP      = build/Tekstvask-mac.zip
NOTARY_PROFILE ?= tekstvask-notary
XCB = xcodebuild -project $(PROJECT) -scheme $(SCHEME) -derivedDataPath $(DERIVED) -quiet

.PHONY: gen app test run install release clean windows windows-test

gen:
	xcodegen generate --quiet

app: gen
	$(XCB) -configuration Release build
	rm -rf $(APP) && ditto $(DERIVED)/Build/Products/Release/Tekstvask.app $(APP)

test: gen
	$(XCB) -destination 'platform=macOS' test

run: app
	open $(APP)

install: app
	rm -rf /Applications/Tekstvask.app && ditto $(APP) /Applications/Tekstvask.app

release: gen
	@test -n "$(TEAM_ID)" || { echo "Usage: make release TEAM_ID=<your Apple team id>"; exit 1; }
	$(XCB) -configuration Release build CODE_SIGN_IDENTITY="Developer ID Application" \
		DEVELOPMENT_TEAM=$(TEAM_ID) OTHER_CODE_SIGN_FLAGS=--timestamp
	rm -rf $(APP) $(ZIP) && ditto $(DERIVED)/Build/Products/Release/Tekstvask.app $(APP)
	ditto -c -k --keepParent $(APP) $(ZIP)
	xcrun notarytool submit $(ZIP) --keychain-profile $(NOTARY_PROFILE) --wait
	xcrun stapler staple $(APP)
	rm $(ZIP) && ditto -c -k --keepParent $(APP) $(ZIP)
	@echo "Ready to share: $(ZIP)"

# Windows app (see windows/): self-contained .exe files, built here or on Windows with the .NET 10 SDK.
WIN_PROJECT = windows/Tekstvask/Tekstvask.csproj

windows:
	@for rid in win-x64 win-arm64; do \
		rm -rf build/$$rid build/Tekstvask-$$rid.zip && \
		dotnet publish $(WIN_PROJECT) -c Release -r $$rid -o build/$$rid -v quiet -nologo && \
		(cd build/$$rid && zip -q ../Tekstvask-$$rid.zip Tekstvask.exe) || exit 1; \
	done
	@echo "Ready to share: build/Tekstvask-win-x64.zip (most PCs), build/Tekstvask-win-arm64.zip (Snapdragon PCs)"

windows-test:
	dotnet test windows/Tekstvask.Tests

clean:
	rm -rf build $(PROJECT) windows/*/bin windows/*/obj
