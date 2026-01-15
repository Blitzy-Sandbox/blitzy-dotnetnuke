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
	''' This script renders all skin widgets defined on the page.
	''' This script requires that init.js be called prior to calling it.
	''' </summary>
	''' <remarks>
	''' </remarks>
	''' <history>
	'''     Version 1.0.0: Oct. 16, 2007, Nik Kalyani, nik.kalyani@dotnetnuke.com 
	''' </history>
	''' -----------------------------------------------------------------------------
*/

////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// S K I N W I D G E T S                                                                                      //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// BEGIN: Namespace management
Type.registerNamespace("DotNetNuke.UI.WebControls.SkinWidgets");
// END: Namespace management



////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// B A S E  W I D G E T                                                                                //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// BEGIN: BaseWidget class                                                                                    //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////
DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget = function(widget)
{
    if (!widget)
        return(null);
    this._widget = widget;
    this._dependencies = [];
    this._readyCounter = 0;
}

DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget.prototype = 
{
        getWidget :
        function()
        {
		    return(this._widget);
        },
        
        setDependency :
        function(className, scriptPath, isResource)
        {
            // Only add the dependency if the class is not already available
            if (typeof(eval(className)) === "undefined")
            {
                $injectScriptElement(scriptPath, isResource);            
                this._dependencies.push(className);
            }
        },

        onReady :
        function(handler, errorHandler, attempts, interval)
        {
            this._handler = handler;
            if (errorHandler)
                this._errorHandler = errorHandler;
            else
                this._errorHandler = "alert('Error loading dependent classes:[ERRORCLASSES]')";
            if ((attempts) && (attempts > 0))
                this._attempts = attempts;
            else 
                this._attempts = 50;

            if ((interval) && (interval > 50))
                this._interval = interval;
            else 
                this._interval = 50;
                
            this._checkIfReady(this);
        },
        
        elementHTML :
        function(element)
        {
            if (element == null)
                return("");

            var _emptyTags = 
            {
               "IMG":   true,
               "BR":    true,
               "INPUT": true,
               "META":  true,
               "LINK":  true,
               "PARAM": true,
               "HR":    true
            };

           var attrs = element.attributes;
           var str = "<" + element.tagName;
           for (var i = 0; i < attrs.length; i++)
              str += " " + attrs[i].name + "=\"" + attrs[i].value + "\"";

           if (_emptyTags[element.tagName])
              return(str + "/>");

           return(str + ">" + element.innerHTML + "</" + element.tagName + ">");
        },
        
        _checkIfReady :
        function(self)
        {
            // Using "self" ensures that window.setTimeout can obtain the right context
            self._readyCounter++;
            if (self._readyCounter > self._attempts)
            {
                var errorClasses = "";
                for(var d = 0; d<self._dependencies.length; d++)
                {
                    if (self._dependencies[d] != "")
                        errorClasses += " " + self._dependencies[d];
                }
                eval(self._errorHandler.replace("[ERRORCLASSES]", errorClasses));
                return;
            }                
            var ready = true;
            for(var d=0; d<self._dependencies.length; d++)
            {
                if (self._dependencies[d] != "")
                {
                    if (typeof(eval(self._dependencies[d])) === "undefined")
                    {
                        ready = false;
                        window.setTimeout(function() { self._checkIfReady(self); }, self._interval);
                        break;
                    }
                    else
                        self._dependencies[d] = "";
                }
            }
            if (ready)
                eval(self._handler);
        },
        
        render :
        function(element)
        {
            element.id = this._widget.id;
            this._widget.parentNode.replaceChild(element, this._widget);                
        }
        
}

DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget.inheritsFrom(Sys.Component);
DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget.registerClass("DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget", Sys.Component);
// END: BaseWidget class


////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// STATIC METHODS                                                                                             //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// BEGIN: renderWidgets
// Detects all the widgets found on the page and loads scripts for each widget class
var $pageSkinWidgets = new Array();
var $pageSkinWidgetClasses = new Array();
var $pageSkinWidgetRenderAttempts = 0;

DotNetNuke.UI.WebControls.SkinWidgets.renderWidgets = function()
{
    var objects = document.getElementsByTagName("object");
    if ((objects == null) || (objects.length == 0)) return;
    // Get all the widget object IDs
    // Need to store the IDs and then process because replacing
    // objects changes the DOM making it impossible to loop
    // through the objects.
    for(var o=0;o<objects.length;o++)
    {
        try
        {
            if (
                    (objects[o].codeType.toLowerCase() == "client") &&
                    (objects[o].id != "") &&
                    (objects[o].childNodes.length > 0)
                )
             {
                $pageSkinWidgets.push(objects[o]);                
                var widgetType = objects[o].className;
                if (typeof($pageSkinWidgetClasses[widgetType]) === "undefined")
                {   
                    if (widgetType.indexOf(".") > -1)
                    {
                        var scriptUrl = "";
                        var params = objects[o].childNodes;
                        for(var p=0;p<params.length;p++)
                        {
                            try
                            {
                                if (params[p].name.toLowerCase() == "scripturl")
                                {
                                    scriptUrl = params[p].value;
                                    break;
                                }
                            }
                            catch(e)
                            {                
                            }
                        }
                        $injectScriptElement(scriptUrl + widgetType + ".js", false);
                        $pageSkinWidgetClasses[widgetType] = widgetType;
                    }
                    else
                    {
                        $injectScriptElement("SkinWidgets/scripts/" + widgetType + ".js", true);
                        $pageSkinWidgetClasses[widgetType] = "DotNetNuke.UI.WebControls.SkinWidgets." + widgetType;
                    }
                }
             }
        }
        catch(e)
        {
        }
    }
    
    if ($pageSkinWidgets.length > 0)
        DotNetNuke.UI.WebControls.SkinWidgets.renderWidgetClasses();
}
// END: renderWidgets    

// BEGIN: renderWidgetClasses
// Makes multiple attempts to render all the widgets found on the page
// while giving dynamically loaded scripts to be processed by browser
DotNetNuke.UI.WebControls.SkinWidgets.renderWidgetClasses = function()
{
    var doneRendering = true;
    $pageSkinWidgetRenderAttempts++;

    // Attempt to render each className
    for(var className in $pageSkinWidgetClasses)
    {
        // Since there are still elements in $pageSkinWidgetClasses, it
        // means there are classes that need to be rendered. The "doneRendering"
        // flag is used to determine if this function should be called again in
        // a few milliseconds to give the browser time to process the dynamically
        // loaded script for unrendered classes.
        doneRendering = false;

        var evalType = "undefined";
        try
        {
            evalType = typeof(eval($pageSkinWidgetClasses[className]));
        }
        catch(e)
        {
            evalType = "undefined";
        }
        if (evalType == "function")
        {
            // Getting here means that the browser has finished loading and processing
            // the script for className. Let's find all the widgets of this class and
            // render them.
            for(var w=0; w<$pageSkinWidgets.length; w++)
            {   
                if ($pageSkinWidgets[w].className != className)
                    continue;
                    
                var widget = eval("new " + $pageSkinWidgetClasses[className] + "($pageSkinWidgets[w])");
                widget.render();
            }

            // Since this className has now been renderd, remove it
            // from the associative array so it is not processed again
            delete $pageSkinWidgetClasses[className];
        }
    }
    
    // Call this function again if there are any unrendered widgets on the page
    if ((!doneRendering) && ($pageSkinWidgetRenderAttempts < 100))
        window.setTimeout(DotNetNuke.UI.WebControls.SkinWidgets.renderWidgetClasses, 100);
}
// END: renderWidgets

DotNetNuke.UI.WebControls.SkinWidgets.renderWidgets();
