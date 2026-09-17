# Staffing Patch Baseline

Unity target: `6000.5.9f1` (`b57deb96f08d`)  
Project: `C:\Users\artwh\BanishedInSpace`

## Commands attempted

```text
Unity.exe -batchmode -nographics -quit -projectPath C:\Users\artwh\BanishedInSpace -runTests -testPlatform editmode -testResults Temp\baseline-editmode.xml -logFile Temp\baseline-editmode.log
```

## Environment result

The batch process reached the Unity licensing client and exited with return code
1 before compilation or test discovery. The log reports `LicenseClient-artwh
refused` and no XML result was produced. This is an environment prerequisite,
not a test pass. Re-run the exact command after licensing is available before
claiming automated acceptance.

## Source baseline captured

- Food production is resource-converter based; no `FarmController` production
  loop is present in the live scripts.
- Staffing publishes `FacilityPerformanceComponent` and the converter consumes
  its operational state/multiplier.
- The pre-patch staffing code deferred away/working assignment changes through
  `pendingEmployment` and permanently cached performance providers.
- Tickable components registered from `Start` and could be lost when a manager
  appeared later.
- Population and vehicle registries also depended on manager/component startup
  order.
- Existing EditMode staffing tests encoded deferred assignment and null-class
  Doctor/Nurse roles; those assertions were rewritten with this patch.

## Required follow-up

Run compile, all EditMode, and all PlayMode suites in a licensed clean editor
process. Replace this file's environment result with actual totals and retain
the licensing log only as historical evidence.

