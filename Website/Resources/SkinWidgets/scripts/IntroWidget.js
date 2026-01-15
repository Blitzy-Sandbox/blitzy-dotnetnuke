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
	''' This is a script that renders an Intro widget that is ideal
	''' for site home or section home pages. It displays links for 
	''' the user to click on to display content. While waiting for
	''' input, it rotates the display of other content.
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
// BEGIN: IntroWidget class                                                                              //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////
DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget = function(widget)
{
    DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.initializeBase(this, [widget]);
    DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.callBaseMethod(this, "setDependency", ["DotNetNuke.UI.WebControls.ContentRotator", "ContentRotator/scripts/ContentRotator.js", true]);
    this._displayHeight = 200;
    this._displayWidth = 400;
    this._displayInterval = -1;
    this._displayDirection = "";
    this._displayFeedUrl = "";
    this._displayFeedAttribute = "description";
    this._displayImageUrl = "";
    this._displayImageTemplate = "photo[INDEX].jpg";
    this._displayImageCount = 0;
    this._displayImageScale = "";
    this._content = [];
    this._displayElementId = "";
    this._scrollerObject = this._widget.id + "_Scroller";
    this._scrollerDisplayElementId = this._widget.id + "_ScrollerDisplay";
    this._overlayDisplayElementId = this._widget.id + "_OverlayDisplay";    
    this._navElementId = this._widget.id + "_Nav";
    
    this._widgetHtml = "";    
}

DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.prototype = 
{        
        // BEGIN: render
        render : 
        function()
        {
            var params = this._widget.childNodes;
            for(var p=0;p<params.length;p++)
            {
                try
                {
                    var paramName = params[p].name.toLowerCase();
                    switch(paramName)
                    {
                        case "text" : this._text = params[p].value; break;
                        case "displayelementid"     : this._displayElementId = params[p].value; break;
                        case "displayheight"        : if (params[p].value > 0) this._displayHeight = params[p].value; break;
                        case "displaywidth"         : if (params[p].value > 0) this._displayWidth = params[p].value; break;
                        case "displayinterval"      : if (params[p].value > 0) this._displayInterval = params[p].value; break;
                        case "displaydirection"     : 
                                                        var dir = params[p].value.toLowerCase();
                                                        if ((dir == "up") || (dir == "down") || (dir == "right") || (dir = "left"))
                                                            this._displayDirection = dir;
                                                        break;    
                        case "displayfeedurl"       : this._displayFeedUrl = params[p].value; break;
                        case "displayfeedattribute" : this._displayFeedAttribute = params[p].value; break;
                        case "displayimageurl"      : this._displayImageUrl = params[p].value; break;
                        case "displayimagetemplate" : if (params[p].value.indexOf("[INDEX]") > -1) 
                                                            this._displayImageTemplate = params[p].value; 
                                                      break;
                        case "displayimagecount"    : if (params[p].value > 0) 
                                                            this._displayImageCount = params[p].value; 
                                                      break;
                        case "displayimagescale"    : if (params[p].value == "width")
                                                            this._displayImageScale = "width";
                                                      else if (params[p].value == "height")
                                                            this._displayImageScale = "height";
                                                      break;
                        case "contentelementid"     : if ($get(params[p].value) != null)
                                                      {
                                                            var contentElement = $get(params[p].value);
                                                            for(var c=0; c<contentElement.childNodes.length; c++)
                                                            { 
                                                                var node = contentElement.childNodes[c];
                                                                if (typeof(node.tagName) === "undefined") continue;
                                                                var content = DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.callBaseMethod(this, "elementHTML", [node]);

                                                                if (content != "")
                                                                    this._content.push(content);
                                                            }
                                                      }
                                                      break;          
                        case "navelementid"         : this._navElementId = params[p].value; break;
                    }
                }
                catch(e)
                {                
                }
            }

            if ((this._displayImageUrl != "") && (this._displayImageCount > 0) && (this._displayImageTemplate != ""))
            {
                var scale = (this._displayImageScale == "width" ? " style=\"width:" + this._displayWidth + "px\""
                          : (this._displayImageScale == "height" ?  " style=\"height:" + this._displayHeight + "px\"" : ""));
                for(var i=1; i<=this._displayImageCount; i++)
                {
                    var newImg = "<img src=\"" + this._displayImageUrl + this._displayImageTemplate.replace("[INDEX]", i) + "\" alt=\"\"" + scale + " />";
                    this._content.push(newImg);
                }
            }
            
            // Wait until all dependent classes are available, then render widget
            DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.callBaseMethod(this, "onReady", ["this._internalRender()"]);
        },
        // END: render

        _internalRender :
        function()
        {
            var div = document.createElement("div");
            var displayElement = null;
            
            if (this._displayElementId != "")
                displayElement = $get(this._displayElementId);
            
            var displayHtml = "<div id=\"" + this._scrollerDisplayElementId + "\"></div>" + 
                              "<div id=\"" + this._overlayDisplayElementId + "\"></div>" +
                              "<div id=\"" + this._navElementId + "\"></div>";
                              
            if (displayElement == null)
            {
                this._displayElementId = this._widget.id;
                div.innerHTML = displayHtml;
            }
            else
                displayElement.innerHTML = displayHtml;
                        
            DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.callBaseMethod(this, "render", [div]);
           // DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.callBaseMethod(this, "render", [div]);
            eval("window." + this._scrollerObject + " = new DotNetNuke.UI.WebControls.ContentRotator.Rotator(" + 
                                                            "\"window." + this._scrollerObject + "\"" + 
                                                            ",\"" + this._scrollerDisplayElementId + "\"" + 
                                                            (this._displayWidth > 0 ? ", " + this._displayWidth : "") +
                                                            (this._displayHeight > 0 ? ", " + this._displayHeight : "") +
                                                            (this._displayDirection != "" ? ", \"" + this._displayDirection + "\"" : "") +
                                                            (this._displayInterval > 0 ? ", " + this._displayInterval : "") +
                                                           ");");
                                                                                                   

            var scroller = eval("window." + this._scrollerObject);
            for(var c=0; c<this._content.length; c++) 
            {          
                var content = this._content[c];                                                                    
                if (content != "")
                    scroller.addContent(content);
            }

            if ((this._displayFeedUrl != "") && (this._displayFeedAttribute != ""))
                scroller.addFeedContent(this._displayFeedUrl,this._displayFeedAttribute);

            scroller.scroll();
            
        }                
}

DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.inheritsFrom(DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget);
DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget.registerClass("DotNetNuke.UI.WebControls.SkinWidgets.IntroWidget", DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget);
// END: IntroWidget class
