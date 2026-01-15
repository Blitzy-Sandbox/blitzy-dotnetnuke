<%@ Control language="vb" AutoEventWireup="false" Explicit="True" Inherits="DotNetNuke.UI.Containers.Container" %>
<%@ Register TagPrefix="dnn" TagName="ACTIONS" Src="~/Admin/Containers/SolPartActions.ascx" %>
<%@ Register TagPrefix="dnn" TagName="ICON" Src="~/Admin/Containers/Icon.ascx" %>
<%@ Register TagPrefix="dnn" TagName="TITLE" Src="~/Admin/Containers/Title.ascx" %>
<%@ Register TagPrefix="dnn" TagName="VISIBILITY" Src="~/Admin/Containers/Visibility.ascx" %>
<%@ Register TagPrefix="dnn" TagName="ACTIONBUTTON" Src="~/Admin/Containers/ActionButton.ascx" %>

        <div class="c_head title_blue">
            <div class="Head title">
                <dnn:ACTIONS runat="server" id="dnnACTIONS"  ProviderName="DNNMenuNavigationProvider" ExpandDepth="1" PopulateNodesFromClient="True" />
                <dnn:ICON runat="server" id="dnnICON" />
                &nbsp;<dnn:TITLE runat="server" id="dnnTITLE"  CssClass="Head" />
            </div>
            <div class="Head title_vis">
                <dnn:VISIBILITY runat="server" id="dnnVISIBILITY"  minicon="images/DNN-minus.gif" maxicon="images/DNN-plus.gif" />
            </div>
            <div class="clear_float"></div>
        </div>
        <div class="c_content">
            <div id="ContentPane"
                 runat="server"
                 class="Normal c_contentpane"></div>
        </div>
        <div class="c_footer">
            <div class="Head">
            	<dnn:ACTIONBUTTON runat="server" id="dnnACTIONBUTTON1"  CommandName="AddContent.Action" DisplayIcon="True" DisplayLink="True" />
            	<dnn:ACTIONBUTTON runat="server" id="dnnACTIONBUTTON2"  CommandName="SyndicateModule.Action" DisplayIcon="True" DisplayLink="false" />
            	<dnn:ACTIONBUTTON runat="server" id="dnnACTIONBUTTON3"  CommandName="PrintModule.Action" DisplayIcon="True" DisplayLink="false" />
            	<dnn:ACTIONBUTTON runat="server" id="dnnACTIONBUTTON4"  CommandName="ModuleSettings.Action" DisplayIcon="True" DisplayLink="false" />
            </div>
        </div>
    
