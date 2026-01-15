function $loadDnnAjaxSharedScript()
{
    if (typeof(DotNetNuke) === "undefined")
    {
        // Wait for init.js to finish registering DotNetNuke.UI.WebControls
        $dnnAjaxSharedScriptLoadAttempts++;
        if ($dnnAjaxSharedScriptLoadAttempts < 50)
            window.setTimeout($loadDnnAjaxSharedScript, 50);
    }
    else
    {
        $injectScriptElement($baseResourcesUrl + "Shared/scripts/DotNetNukeAjaxShared.js");
        $loadSkinWidgetsScript();
    }
}

function $loadSkinWidgetsScript()
{
    if (typeof(DotNetNuke.UI.WebControls.Utility) === "undefined")
    {
        // Wait for DotNetNukeAjaxShared.js to finish registering DotNetNuke.UI.WebControls.Utility
        $skinWidgetsScriptLoadAttempts++;
        if ($skinWidgetsScriptLoadAttempts < 50)
            window.setTimeout($loadSkinWidgetsScript, 100);
    }
    else
        $injectScriptElement($baseResourcesUrl + "SkinWidgets/scripts/SkinWidgets.js");
}


// Ensure that Microsoft AJAX base script is loaded     
if (typeof($injectScriptElement) != "function")
{
    var msAjaxInit = document.createElement("script");
    msAjaxInit.type = "text/javascript";
    msAjaxInit.src = $dnnHostUrl + "Resources/Shared/scripts/init.js";
    document.getElementsByTagName("head")[0].appendChild(msAjaxInit);
}

// Load the DotNetNukeAjaxShared script
var $dnnAjaxSharedScriptLoadAttempts = 0;
var $skinWidgetsScriptLoadAttempts = 0;
$loadDnnAjaxSharedScript();

