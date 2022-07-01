KPDir = /usr/lib/keepass2
KPPDir = $(KPDir)/Plugins
NUDIR = packages
PLGXDIR = plugin

all: $(PLGXDIR)/KeePassFaviconDownloader.plgx

$(PLGXDIR)/KeePassFaviconDownloader.plgx: KeePassFaviconDownloader/KeePassFaviconDownloader.csproj \
								  KeePassFaviconDownloader/src/KeePassFaviconDownloaderExt.cs \
								  KeePassFaviconDownloader/src/Properties/AssemblyInfo.cs \
								  KeePassFaviconDownloader/lib/HtmlAgilityPack.dll
	mono "$(KPDir)/KeePass.exe" --plgx-create "KeePassFaviconDownloader"
	\mkdir -p "$(PLGXDIR)"
	\mv -f KeePassFaviconDownloader.plgx "$(PLGXDIR)/KeePassFaviconDownloader.plgx"

KeePassFaviconDownloader/lib/HtmlAgilityPack.dll:
	nuget install -o "$(NUDIR)" ./KeePassFaviconDownloader/packages.config
	\cp -f "$(NUDIR)/$$( ls "$(NUDIR)" | grep HtmlAgilityPack | head -1 )/lib/Net20/HtmlAgilityPack.dll" KeePassFaviconDownloader/lib/HtmlAgilityPack.dll

install: $(PLGXDIR)/KeePassFaviconDownloader.plgx
	install -D -m 644 "$(PLGXDIR)/KeePassFaviconDownloader.plgx" "$(DESTDIR)$(KPPDir)/KeePassFaviconDownloader.plgx"

clean:
	\rm -rf "$(NUDIR)" "KeePassFaviconDownloader/lib/HtmlAgilityPack.dll" "$(PLGXDIR)"

distclean: clean

uninstall:
	\rm -f "$(DESTDIR)$(KPPDir)/KeePassFaviconDownloader.plgx"

.PHONY: all install clean distclean uninstall
