#We have to rerun the coverage results and export to HTML to get the coverage percentage
#Run the coverage results in Visual Studio and export the snapshot TestResultCoverage.html 
dotnet sonarscanner begin -k:"Vts.MonteCarlo" /o:"lmalenfant" /d:sonar.host.url=https://sonarcloud.io /d:sonar.login="464ae516e59fb7c8f6add06984f1f9e8b711d5ce" /d:sonar.c.file.suffixes=- /d:sonar.cpp.file.suffixes=- /d:sonar.objc.file.suffixes=- /d:sonar.cs.nunit.reportsPaths=TestResult.trx /d:sonar.cs.dotcover.reportsPaths=TestResultCoverage.html
dotnet build $PWD\Vts.MonteCarlo.sln /t:Rebuild /p:Configuration=Release
dotnet test $PWD\Vts.MonteCarlo.sln -c:Release -l:"trx;LogFileName=TestResult.trx"
dotnet sonarscanner end /d:sonar.login="464ae516e59fb7c8f6add06984f1f9e8b711d5ce"
Read-Host -Prompt "Press Enter to exit"