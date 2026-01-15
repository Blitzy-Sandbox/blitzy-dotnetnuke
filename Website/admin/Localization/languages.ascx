<%@ Control Language="vb" AutoEventWireup="false" Inherits="DotNetNuke.Services.Localization.Languages"
    CodeFile="Languages.ascx.vb" %>
<%@ Register TagPrefix="dnn" TagName="SectionHead" Src="~/controls/SectionHeadControl.ascx" %>
<%@ Register TagPrefix="dnn" TagName="Label" Src="~/controls/LabelControl.ascx" %>
<dnn:SectionHead ID="dshBasic" runat="server" CssClass="Head" Text="Suported Locales"
    Section="tblBasic" ResourceKey="SupportedLocales" IncludeRule="False"></dnn:SectionHead>
<br />
<table id="tblBasic" cellspacing="1" cellpadding="1" border="0" runat="server">
    <tr>
        <td colspan="2">
            <asp:DataGrid ID="dgLocales" runat="server" CssClass="Normal" AutoGenerateColumns="False"
                CellPadding="4" GridLines="None">
                <AlternatingItemStyle Wrap="False"></AlternatingItemStyle>
                <ItemStyle Wrap="False"></ItemStyle>
                <HeaderStyle Font-Bold="True" Wrap="False"></HeaderStyle>
                <Columns>
                    <asp:BoundColumn DataField="name" ReadOnly="True" HeaderText="Name"></asp:BoundColumn>
                    <asp:BoundColumn DataField="key" ReadOnly="True" HeaderText="Key"></asp:BoundColumn>
                    <asp:TemplateColumn HeaderText="Status">
                        <ItemTemplate>
                            <asp:Label ID="lblStatus" runat="server" CssClass="Normal"></asp:Label>
                        </ItemTemplate>
                    </asp:TemplateColumn>
                    <asp:TemplateColumn>
                        <ItemTemplate>
                            <asp:LinkButton ID="cmdDisable" runat="server" CssClass="CommandButton" Text="Disable"
                                CausesValidation="false" CommandName="Disable"></asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateColumn>
                    <asp:TemplateColumn>
                        <ItemTemplate>
                            <asp:LinkButton ID="cmdDelete" runat="server" resourcekey="cmdDelete" CssClass="CommandButton"
                                Text="Delete" CommandName="Delete" CausesValidation="False" Visible="<%# UserInfo.IsSuperUser %>">Delete</asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateColumn>
                </Columns>
            </asp:DataGrid>
            <asp:CheckBox ID="chkDeleteFiles" runat="server" resourcekey="DeleteFiles" CssClass="Normal"
                Text="Delete all resources of this locale, too" TextAlign="left"></asp:CheckBox>
        </td>
    </tr>
    <tr>
		<td style="width:250px" class="SubHead">
            <dnn:Label ID="lblEnableBrowser" runat="server" ControlName="chkEnableBrowser"></dnn:Label>
		</td>
		<td>
            <asp:CheckBox ID="chkEnableBrowser" runat="server" CssClass="SubHead" AutoPostBack="True" TextAlign="left" />
        </td>
    </tr>
    <tr>
		<td style="width:250px" class="SubHead">
             <dnn:Label ID="lblEnableLanguageInUrl" runat="server" ControlName="chkEnableLanguageInUrl"></dnn:Label>
		</td>
		<td>
             <asp:CheckBox ID="chkEnableLanguageInUrl" runat="server" CssClass="SubHead" AutoPostBack="True" TextAlign="left" />
        </td>
    </tr>
</table>
<asp:Panel ID="pnlAdd" runat="server">
    <p>
        <dnn:SectionHead ID="dshAdd" runat="server" CssClass="Head" Text="Add New Locale"
            Section="tblAdd" ResourceKey="AddNewLocale" IncludeRule="False"></dnn:SectionHead>
        <br />
        <table id="tblAdd" cellspacing="1" cellpadding="1" border="0" runat="server">
            <tr>
                <td >
                </td>
                <td >
                    <asp:RadioButtonList ID="rbDisplay" runat="server" CssClass="Normal" RepeatDirection="Horizontal"
                        RepeatLayout="Flow" AutoPostBack="True">
                        <asp:ListItem Value="English" resourcekey="DisplayEnglish">English</asp:ListItem>
                        <asp:ListItem Value="Native" resourcekey="DisplayNative" Selected="True">Native</asp:ListItem>
                    </asp:RadioButtonList></td>
            </tr>
            <tr>
                <td class="SubHead" valign="top" style="width:150px">
                    <dnn:Label ID="lbLocale" runat="server" Text="Name" ControlName="txtName"></dnn:Label>
                </td>
                <td valign="top" >
                    <asp:DropDownList ID="cboLocales" runat="server">
                    </asp:DropDownList></td>
            </tr>
            <tr>
                <td >
                </td>
                <td >
                    <asp:LinkButton ID="cmdAdd" runat="server" resourcekey="Add" CssClass="CommandButton">Add</asp:LinkButton></td>
            </tr>
        </table>
    </p>
</asp:Panel>
