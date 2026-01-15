<%@ Control language="vb" AutoEventWireup="false" Explicit="True" Inherits="DotNetNuke.Modules.Admin.Tabs.Import" CodeFile="Import.ascx.vb" %>
<%@ Register TagPrefix="dnn" TagName="Label" Src="~/controls/LabelControl.ascx" %>
<table style="width:560px;" cellspacing="2" cellpadding="2" border="0" summary="Edit Links Design Table">
    <tr>
        <td class="SubHead" style="vertical-align:top; width:150px;"><dnn:label id="plFolder" runat="server" controlname="cboFolders" /></td>
        <td><asp:DropDownList ID="cboFolders" Runat="server" CssClass="NormalTextBox" Width="300" AutoPostBack="true" /></td>
    </tr>
	<tr>
		<td class="SubHead" style="vertical-align:top; width:150px;"><dnn:label id="plTemplate" runat="server" controlname="cboTemplate" /></td>
		<td style="vertical-align:top;">
		    <asp:dropdownlist id="cboTemplate" cssclass="NormalTextBox" runat="server" width="300" AutoPostBack="True" />
			<asp:RequiredFieldValidator id="valTemplate" runat="server" Display="Dynamic" ControlToValidate="cboTemplate" InitialValue="-1" resourcekey="valTemplate.ErrorMessage"/>
			<br/>
			<asp:Label id="lblTemplateDescription" runat="server" CssClass="Normal" /></td>
	</tr>
	<tr>
		<td class="SubHead" style="vertical-align:top; width:150px;"><dnn:label id="plMode" runat="server" controlname="optMode" /></td>
		<td>
			<asp:radiobuttonlist id="optMode" cssclass="SubHead" runat="server" repeatdirection="Horizontal" repeatlayout="Flow" autopostback="True">
				<asp:listitem value="ADD" resourcekey="ModeAdd" />
				<asp:listitem value="REPLACE" resourcekey="ModeReplace" />
			</asp:radiobuttonlist>
		</td>
	</tr>
	<tr id="trTabName" runat="server" visible="false">
		<td class="SubHead" style="vertical-align:top; width:150px;"><dnn:label id="plTabName" runat="server" controlname="txtTabName" /></td>
		<td><asp:TextBox id="txtTabName" cssclass="NormalTextBox" runat="server" maxlength="50" width="300" /></td>
	</tr>
	<tr>
		<td class="SubHead" style="vertical-align:top; width:150px;"><dnn:label id="plRedirect" runat="server" controlname="optRedirect" /></td>
		<td>
			<asp:radiobuttonlist id="optRedirect" cssclass="SubHead" runat="server" repeatdirection="Horizontal" repeatlayout="Flow">
				<asp:listitem value="VIEW" resourcekey="ModeView" />
				<asp:listitem value="SETTINGS" resourcekey="ModeSettings" />
			</asp:radiobuttonlist>
		</td>
	</tr>
</table>
<p>
    <asp:linkbutton id="cmdImport" resourcekey="cmdImport" runat="server" cssclass="CommandButton" text="Import" borderstyle="none"></asp:linkbutton>&nbsp;
    <asp:linkbutton id="cmdCancel" resourcekey="cmdCancel" runat="server" cssclass="CommandButton" text="Cancel" borderstyle="none" causesvalidation="False"></asp:linkbutton>
</p>
