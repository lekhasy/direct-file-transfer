publish-server:
	dotnet publish direct-file-transfer/direct-file-transfer.csproj -c Release

publish-server-arm:
	dotnet publish direct-file-transfer/direct-file-transfer.csproj -c Release -r win-arm64 --self-contained true

publish-client:
	dotnet publish direct-file-transfer-client/direct-file-transfer-client.csproj -c Release

publish-all:
	make publish-server-arm
	make publish-client
