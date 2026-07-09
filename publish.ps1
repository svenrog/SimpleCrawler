foreach ($b in 'HtmlAgilityPack','V8','AngleSharp','Jint','Playwright','Puppeteer') {
    Remove-Item -Recurse -Force src/SimpleCrawler.Console/bin, src/SimpleCrawler.Console/obj -ErrorAction SilentlyContinue
    dotnet publish src/SimpleCrawler.Console -c Release -r win-x64 --self-contained -p:CrawlerBackend=$b -o publish/$b
}
