KPDir = /usr/lib/keepass2
KPPDir = $(KPDir)/plugins
BuildDir = build

all: KeePassFaviconDownloader.plgx

KeePassFaviconDownloader.plgx: KeePassFaviconDownloader.csproj \
							   KeePassFaviconDownloaderExt.cs \
							   Properties/AssemblyInfo.cs \
							   HtmlAgilityPack.dll
	mkdir -p "$(BuildDir)"
	\cp -f "KeePassFaviconDownloader.csproj" "$(BuildDir)/"
	\cp -f "KeePassFaviconDownloaderExt.cs" "$(BuildDir)/"
	\cp -f "Properties/AssemblyInfo.cs" "$(BuildDir)/"
	\cp -f "HtmlAgilityPack.dll" "$(BuildDir)/"
	mono "$(KPDir)/KeePass.exe" --plgx-create "$(BuildDir)"
	\mv -f "$(BuildDir).plgx" "$(BuildDir)/KeePassFaviconDownloader.plgx"

install: KeePassFaviconDownloader.plgx
	\mkdir -p "$(DESTDIR)$(KPPDir)"
	install -D "$(BuildDir)/KeePassFaviconDownloader.plgx" \
		"$(DESTDIR)$(KPPDir)/KeePassFaviconDownloader.plgx"

clean:
	\rm -rf "$(BuildDir)"

distclean: clean

uninstall:
	\rm -f "$(DESTDIR)$(KPPDir)/KeePassFaviconDownloader.plgx"

.PHONY: all install clean distclean uninstall