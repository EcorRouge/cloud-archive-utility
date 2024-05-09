:: !!! WARNING !!! 
:: Use this script with caution - review the updated cloud_connectors_package_ids.txt and make sure it only contains trusted packages.
:: This is because a potential attacker can upload malicious package which matches the search pattern. You don't want to load that.
:: A potential future improvement here is to only use signed packages, and implement signature verification.

:: Also note that the script specifies concrete versions in the cloud_connectors_package_ids.txt.
:: You may want use * instead of the concrete version - to point to the latest. 
:: This is especially useful during the development, if pointing to the local nuget packages source.

:: Pull ids of the structured connectors packages from the Nuget package feed, excluding Reveles.Collector.Cloud.Connector package containing base abstractions
nuget.exe search "Reveles.Cloud.Connector." -Source "https://api.nuget.org/v3/index.json" -Take 100 -NonInteractive -Verbosity quiet ^
	| findstr /V "Reveles.Collector.Cloud.Connector" ^
	| findstr /c:Cloud\.Connector > ids-raw.txt

del cloud_connectors_package_ids.txt
for /F "tokens=2,4" %%A in (ids-raw.txt) do echo %%A %%B>> cloud_connectors_package_ids.txt
del ids-raw.txt