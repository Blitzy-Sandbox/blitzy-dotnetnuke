var SKIN_DESIGN_MODE_SCRIPTS_HOST_URL = (typeof(SKIN_DESIGN_MODE_SCRIPTS_HOST_URL) === "undefined" ? "http://feeds.DotNetNuke.com/Public/1.00/" : SKIN_DESIGN_MODE_SCRIPTS_HOST_URL);
var SKIN_LIVE_MODE_SCRIPTS_HOST_URL = (typeof(SKIN_LIVE_MODE_SCRIPTS_HOST_URL) === "undefined" ? SKIN_DESIGN_MODE_SCRIPTS_HOST_URL : SKIN_LIVE_MODE_SCRIPTS_HOST_URL);
if (SKIN_LIVE_MODE_SCRIPTS_HOST_URL.indexOf("%=") == 1)
    SKIN_LIVE_MODE_SCRIPTS_HOST_URL = SKIN_DESIGN_MODE_SCRIPTS_HOST_URL;
var $dnnHostUrl = (typeof($dnnHostUrl) === "undefined" ? SKIN_LIVE_MODE_SCRIPTS_HOST_URL : $dnnHostUrl);
var $skinWidgetsLoader = document.createElement("script");
$skinWidgetsLoader.type = "text/javascript";
$skinWidgetsLoader.src = $dnnHostUrl + "Resources/SkinWidgets/scripts/SkinWidgetsLoader.js";
document.getElementsByTagName("head")[0].appendChild($skinWidgetsLoader);
