namespace Potato.Domain.Tests.TestData;

public static class SampleAcfFiles
{
    public const string CloudpunkAcf = """"
"AppState"
{
	"appid"		"746850"
	"Universe"		"1"
	"name"		"Cloudpunk"
	"StateFlags"		"4"
	"installdir"		"Cloudpunk"
	"LastUpdated"		"1785622443"
	"LastPlayed"		"0"
	"SizeOnDisk"		"7087768157"
	"StagingSize"		"0"
	"buildid"		"8245592"
	"LastOwner"		"76561199083839651"
	"DownloadType"		"3"
	"UpdateResult"		"0"
	"BytesToDownload"		"69026464"
	"BytesDownloaded"		"69026464"
	"BytesToStage"		"69021181"
	"BytesStaged"		"69021181"
	"TargetBuildID"		"0"
	"AutoUpdateBehavior"		"0"
	"AllowOtherDownloadsWhileRunning"		"0"
	"ScheduledAutoUpdate"		"0"
	"InstalledDepots"
	{
		"746851"
		{
			"manifest"		"5225699216215765938"
			"size"		"7087768157"
		}
	}
	"UserConfig"
	{
		"language"		"english"
	}
	"MountedConfig"
	{
		"language"		"english"
	}
}
"""";

    public const string MultiDepotAcf = """"
"AppState"
{
	"appid"		"228980"
	"Universe"		"1"
	"name"		"Steamworks Common Redistributables"
	"StateFlags"		"4"
	"installdir"		"Steamworks Shared"
	"LastUpdated"		"1785616685"
	"LastPlayed"		"0"
	"SizeOnDisk"		"812569771"
	"StagingSize"		"0"
	"buildid"		"24098430"
	"LastOwner"		"76561199083839651"
	"DownloadType"		"1"
	"UpdateResult"		"0"
	"BytesToDownload"		"0"
	"BytesDownloaded"		"0"
	"BytesToStage"		"0"
	"BytesStaged"		"0"
	"TargetBuildID"		"24098430"
	"AutoUpdateBehavior"		"0"
	"AllowOtherDownloadsWhileRunning"		"0"
	"ScheduledAutoUpdate"		"0"
	"InstalledDepots"
	{
		"228981"
		{
			"manifest"		"7613356809904826842"
			"size"		"5884085"
		}
		"228982"
		{
			"manifest"		"6413394087650432851"
			"size"		"9688647"
		}
		"228983"
		{
			"manifest"		"8124929965194586177"
			"size"		"19265607"
		}
	}
	"InstallScripts"
	{
		"228981"		"_CommonRedist\\vcredist\\2005\\installscript.vdf"
		"228982"		"_CommonRedist\\vcredist\\2008\\installscript.vdf"
	}
	"UserConfig"
	{
		"platform_override_dest"		"linux"
		"platform_override_source"		"windows"
	}
	"MountedConfig"
	{
		"platform_override_dest"		"linux"
		"platform_override_source"		"windows"
	}
}
"""";

    public const string WithCustomFieldsAcf = """"
"AppState"
{
	"appid"		"108600"
	"name"		"Project Zomboid"
	"installdir"		"ProjectZomboid"
	"buildid"		"123456"
	"CustomKey"		"CustomValue"
	"CustomObject"
	{
		"SubKey"		"SubValue"
	}
	"InstalledDepots"
	{
		"108601"
		{
			"manifest"		"1111222233334444"
			"size"		"2048000"
		}
	}
}
"""";
}
