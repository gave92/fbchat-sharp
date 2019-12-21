param([string]$target)

"Start running appcert toolkit"

# #######################################
# 
# variable preparation
#
# #######################################

# appcert toolkit path
$appcertPath = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\"
$appcert = $appcertPath + "\appcert.exe"

# xap package to test
$xap = "$env:APPVEYOR_BUILD_FOLDER\" + $target

# path to output report
$reportFile = "$env:APPVEYOR_BUILD_FOLDER\examples\uwpapp\appcert.xml"

# #######################################
# 
# run appcert
#
# #######################################

# delete the report file
Remove-Item $reportFile

# run appcert
Start-Process -FilePath $appcert -ArgumentList "reset" -NoNewWindow -Wait
Start-Process -FilePath $appcert -ArgumentList "test -appxpackagepath $xap -reportoutputpath $reportFile" -NoNewWindow -Wait
Start-Process -FilePath $appcert -ArgumentList "reset" -NoNewWindow -Wait

# #######################################
# 
# analyze report
#
# #######################################

[xml]$report = get-content $reportFile

$fail = $FALSE
$reqsWithTests = $report.SelectNodes("//REQUIREMENT[TEST]")
foreach ($req in $reqsWithTests) {
	"------------------------------"
	"Requirement " + $req.NUMBER
	"    Title:     " + $req.TITLE
	"    Rationale: " + $req.RATIONALE
	""
	$tests = $req.SelectNodes("TEST")
	foreach ($test in $tests) {	
		Write-Host "    TEST " -NoNewLine
		Write-Host $test.INDEX -NoNewLine
		Write-Host " " -NoNewLine
		
		# check and print the test result
		$result = "PASS"
		if ($test.RESULT.InnerText -contains 'PASS') {
			Write-Host "PASS" -ForegroundColor "green" -BackgroundColor "black"
			$result = "PASS"
		} else {
			Write-Host "FAIL" -ForegroundColor "red" -BackgroundColor "black"
			$fail = $TRUE
			$result = "FAIL"
		}
		
		# print name and desc
		"        Name:        " + $test.NAME
		"        Description: " + $test.DESCRIPTION
		
		# print the error messages in case the test failed
		$messages = $test.SelectNodes("*/MESSAGE")
		foreach ($message in $messages) {
			Write-Host "        " -NoNewLine
			Write-Host $message.Text -ForegroundColor "red" -BackgroundColor "black"
		}
	}
}
"------------------------------"

# throw an exception in case of any error
if ($fail) {
	throw [System.Exception] "Appcert failed."
}
