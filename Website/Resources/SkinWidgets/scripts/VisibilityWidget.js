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
	''' This script renders all Visibility widgets defined on the page.
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
// BEGIN: VisibilityWidget class                                                                              //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////
DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget = function(widget)
{
    DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.initializeBase(this, [widget]);
}

DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.prototype = 
{        
        // BEGIN: render
        render : 
        function()
        {
            var widgetHtml = "";
            var params = this._widget.childNodes;
            var expandClassName = "expand";
            var collapseClassName = "collapse";
            var targetElementId = "";
            var title = "";
            for(var p=0;p<params.length;p++)
            {
                try
                {
                    var paramName = params[p].name.toLowerCase();
                    switch(paramName)
                    {
                        case "expandclassname" : expandClassName = params[p].value; break;
                        case "collapseclassname" : collapseClassName = params[p].value; break;
                        case "targetelementid"  : targetElementId = params[p].value; break;
                        case "title" : title = params[p].value; break;
                    }
                }
                catch(e)
                {                
                }
            }
            if (targetElementId != "")
            {
                var div = document.createElement("div");
                div.className = expandClassName;
                div.title = title;
                div.setAttribute("expandClassName", expandClassName);
                div.setAttribute("collapseClassName", collapseClassName);
                div.setAttribute("targetElementId", targetElementId);
                $addHandler(div, "click", DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.toggleVisibility);
                DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.callBaseMethod(this, "render", [div]);
            }            
        }                
        // END: render
}

DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.toggleVisibility = function(sender)
{
    var iconObject = sender.target;
    if (iconObject.getAttribute("targetElementId") == null) return;

    var toolbox = $get(iconObject.getAttribute("targetElementId"));
    var expandClassName = iconObject.getAttribute("expandClassName");
    var collapseClassName = iconObject.getAttribute("collapseClassName");
    
    if (iconObject.className == expandClassName)
    {
        iconObject.className = collapseClassName;
        toolbox.style.display = "block";
    }
    else
    {
        iconObject.className = expandClassName;
        toolbox.style.display = "none";
    }
}
DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.inheritsFrom(DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget);
DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget.registerClass("DotNetNuke.UI.WebControls.SkinWidgets.VisibilityWidget", DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget);
// END: VisibilityWidget class
