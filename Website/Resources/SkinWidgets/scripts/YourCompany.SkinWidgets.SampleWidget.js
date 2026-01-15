////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// BEGIN: SampleWidget class                                                                              //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// BEGIN: Namespace management
Type.registerNamespace("YourCompany.SkinWidgets");
// END: Namespace management



YourCompany.SkinWidgets.SampleWidget = function(widget)
{
    YourCompany.SkinWidgets.SampleWidget.initializeBase(this, [widget]);
}

YourCompany.SkinWidgets.SampleWidget.prototype = 
{        
        // BEGIN: render
        render : 
        function()
        {
            var params = this._widget.childNodes;
            var text = "Default Text";
            for(var p=0;p<params.length;p++)
            {
                try
                {
                    var paramName = params[p].name.toLowerCase();
                    switch(paramName)
                    {
                        case "text" : text = params[p].value; break;
                    }
                }
                catch(e)
                {                
                }
            }
            var div = document.createElement("div");
            div.setAttribute("style", "width:100px;height:100px;border:solid 4px red");
            div.innerHTML = text;
            $addHandler(div, "click", YourCompany.SkinWidgets.SampleWidget.clickHandler);
            YourCompany.SkinWidgets.SampleWidget.callBaseMethod(this, "render", [div]);
        }                
        // END: render
}

YourCompany.SkinWidgets.SampleWidget.clickHandler = function(sender)
{
    var clickedObject = sender.target;
    alert("The widget with ID=" + clickedObject.id + " contains text \"" + clickedObject.innerHTML + "\"");
}

YourCompany.SkinWidgets.SampleWidget.inheritsFrom(DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget);
YourCompany.SkinWidgets.SampleWidget.registerClass("YourCompany.SkinWidgets.SampleWidget", DotNetNuke.UI.WebControls.SkinWidgets.BaseWidget);
// END: SampleWidget class
