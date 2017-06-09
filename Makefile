KPDir = /usr/lib/keepass2
KPPDir = $(KPDir)/plugins

all: KeePassFaviconDownloader.plgx

KeePassFaviconDownloader.plgx: src/KeePassFaviconDownloader.csproj \
							   src/KeePassFaviconDownloaderExt.cs \
							   src/Properties/AssemblyInfo.cs \
							   src/HtmlAgilityPack.dll
	mono "$(KPDir)/KeePass.exe" --plgx-create "src"
	\mv -f "src.plgx" "KeePassFaviconDownloader.plgx"

install: KeePassFaviconDownloader.plgx
	\mkdir -p "$(DESTDIR)$(KPPDir)"
	install -D "KeePassFaviconDownloader.plgx" \
		"$(DESTDIR)$(KPPDir)/KeePassFaviconDownloader.plgx"

clean:
	\rm -rf "KeePassFaviconDownloader.plgx"

distclean: clean

uninstall:
	\rm -f "$(DESTDIR)$(KPPDir)/KeePassFaviconDownloader.plgx"

.PHONY: all install clean distclean uninstall