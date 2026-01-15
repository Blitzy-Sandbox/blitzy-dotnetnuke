/*
  DotNetNuke® - http://www.dotnetnuke.com
  Copyright (c) 2002-2008
  by DotNetNuke Corporation
 
  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
  documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
  the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
  to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 
  The above copyright notice and this permission notice shall be included in all copies or substantial portions 
  of the Software.
 
  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
  TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
  THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
  CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
  DEALINGS IN THE SOFTWARE.

	''' -----------------------------------------------------------------------------
	''' <summary>
	''' This script renders all StyleSheet widgets defined on the page.
	''' This script is designed to be called by StyleSheetWidget.js.
	''' Calling it directly will produce an error.
	''' </summary>
	''' <remarks>
	''' </remarks>
	''' <history>
	'''     Version 1.0.0: Oct. 28, 2007, Nik Kalyani, nik.kalyani@dotnetnuke.com 
	''' </history>
	''' -----------------------------------------------------------------------------
*/

////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// BEGIN: StyleSheetWidget class                                                                              //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////
DotNetNuke.UI.WebControls.SkinWidgets.StyleSheetWidget = function(widget)
{
    DotNetNuke.UI.WebControls.SkinWidgets.StyleSheetWidget.initializeBase(this, [widget]);
}

DotNetNuke.UI.WebControls.SkinWidgets.StyleSheetWidget.activateStyleSheet = function(prefix, styleSheetName)
{
    var styleSheets = document.styleSheets;
    var styleSheetTitle = prefix + styleSheetName;
    if (styleSheets.length > 0)
    {
        for(var l=0;l<styleSheets.length;l++)
        {
            var currentStyleSheet = styleSheets[l];
            if (currentStyleSheet.title == styleSheetTitle)
                currentStyleSheet.disabled = false;
            else 
            {
                if (currentStyleSheet.title.indexOf(prefix) == 0)
                     currentStyleSheet.disabled = true;
            }
        }
    }
}

DotNetNuke.UI.WebControls.SkinWidgets.StyleSheetWidget.prototype = 
{
        
        // BEGIN: render
        render : 
        function()
        {
            var widgetHtml = "";
            var cookieValue = "";
            var head = document.getElementsByTagName("head")[0];
            var params = this._widget.childNodes;
            cookieName = "StyleSheetWidget_" + this._widget.id;
            cookieValue = "layouts-1024";        
            var category = this._widget.id.toLowerCase();
            var template = "";
            var baseUrl = "";
            var selectedCssClass = "StyleSheetWidget-Selected";
            var cssClass = "StyleSheetWidget";
            for(var p=0;p<params.length;p++)
            {
                try
                {
                    var paramName = params[p].name.toLowerCase();
                    switch(paramName)
                    {
                        case "template" : template = params[p].value; break;
                        case "baseurl"  : baseUrl = params[p].value; break;
                        case "cssclass" : cssClass = params[p].value; break;
                        case "selectedcssclass" : selectedCssClass = params[p].value; break;
                    }
                }
                catch(e)
                {                
                }
            }
            if (template == "")
                template = "<a href=\"javascript:void(0)\" [CLASS] [ID]>[TEXT]</a> ";

            if (baseUrl.substring(baseUrl.length-1,1) != "/")
                baseUrl += "/";

            for(var p=0;p<params.length;p++)
            {
                var param = params[p];
                var tag = "";
                var name = "";
                try
                {
                    tag = param.tagName.toLowerCase();
                    name = param.name;
                }
                catch(e)
                {
                }
         
                var lcName = name.toLowerCase()       
                if ((tag == "param") && (lcName != "template") && (lcName != "baseurl") && (lcName != "cssclass") && (lcName != "selectedcssclass"))
                {
                    var styleSheetPrefix = "StyleSheetWidget_" + category + "_";
                    var styleSheetName = styleSheetPrefix + param.value;
                    var widgetClass = " class=\"" + (cookieValue == param.value ? "StyleSheetWidget-Selected" : "StyleSheetWidget") + "\"";
                    var widgetEvent = " onclick=\"DotNetNuke.UI.WebControls.SkinWidgets.StyleSheetWidget.activateStyleSheet('" + styleSheetPrefix + "','" + param.value + "')\"";
                    var cleanParam = param.value.replace(/ /g, "-");
                    cleanParam = cleanParam.replace(/\//g, "-");
                    var widgetId = " id=\"" + category + "_" + cleanParam + "\"" + widgetEvent;
                    var html = template.replace(/\[CLASS\]/gi, widgetClass);
                    html = html.replace(/\[ID\]/gi, widgetId);
                    html = html.replace(/\[TEXT\]/gi, param.name);
                    widgetHtml += html;
                    
                    var newStyleSheet = document.createElement("link");
                    newStyleSheet.rel = "alternate stylesheet";
                    newStyleSheet.type = "text/css";
                    newStyleSheet.title = styleSheetName;
                    newStyleSheet.href = baseUrl + param.value + ".css";
                    newStyleSheet.disabled = true;
                    head.appendChild(newStyleSheet);
                }
            }
            var div = document.createElement("div");
            div.innerHTML = widgetHtml;
            DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.callBaseMethod(this, "render", [div]);
        }                
        // END: render
}
DotNetNuke.UI.WebControls.SkinWidgets.StyleSheetWidget.registerClass("DotNetNuke.UI.WebControls.SkinWidgets.StyleSheetWidget", DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget);
// END: StyleSheetWidget class
