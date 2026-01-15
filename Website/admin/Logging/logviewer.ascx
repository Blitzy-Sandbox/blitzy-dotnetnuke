<%@ Register TagPrefix="dnnsc" Namespace="DotNetNuke.UI.WebControls" Assembly="DotNetNuke" %>
<%@ Register TagPrefix="dnn" TagName="SectionHead" Src="~/controls/SectionHeadControl.ascx" %>
<%@ Register TagPrefix="dnn" TagName="Label" Src="~/controls/LabelControl.ascx" %>
<%@ Control CodeFile="LogViewer.ascx.vb" Language="vb" AutoEventWireup="false" Explicit="true" Inherits="DotNetNuke.Modules.Admin.Log.LogViewer" TargetSchema="http://schemas.microsoft.com/intellisense/ie5" %>

<script language="JavaScript" type="text/javascript">
<!--
function CheckExceptions()
{
    var j,isChecked = false;
    if (document.forms[0].item("Exception").length)
        {
            j=document.forms[0].item("Exception").length;
            for (var i=0;i<j;i++)
                {
                    if (document.forms[0].item("Exception")(i).checked==true)
                    {
                        isChecked = true;
                    }
                }
            if (isChecked!=true)
                {
                    alert('Please select at least one exception.');
                }
            return isChecked;
        }
    else 
        {
            if (document.forms[0].item("Exception").checked)
                return true;
            else
                {
                alert('Please select at least one exception.');
                return false;
                }
        }
}

function flipFlop(eTarget) {
    if (document.getElementById(eTarget).style.display=='')
    {
    	document.getElementById(eTarget).style.display='none';
    }
    else
    {
    	document.getElementById(eTarget).style.display='';
    }
}
  
//-->
</script>

<asp:Panel ID="pnlOptions" runat="server">
    <table width="100%" border="0">
        <tr>
            <td valign="top">
                <dnn:SectionHead ID="dshSettings" runat="server" CssClass="Head" ResourceKey="Settings"
                    Section="tblSettings" Text="Viewer Settings"></dnn:SectionHead>
                <table id="tblSettings" cellspacing="2" cellpadding="2" border="0" runat="server">
                    <tr>
                        <td class="SubHead" nowrap="nowrap" align="left" width="110">
                            <dnn:Label ID="plPortalID" runat="server" ControlName="ddlPortalid" Suffix=":"></dnn:Label>
                        </td>
                        <td width="60">
                            <asp:DropDownList ID="ddlPortalid" runat="server" AutoPostBack="true">
                            </asp:DropDownList>
                        </td>
                        <td width="25">
                            &nbsp;
                        </td>
                        <td class="SubHead" align="left" width="100">
                            <dnn:Label ID="plLogType" runat="server" ControlName="ddlLogType" Suffix=":"></dnn:Label>
                        </td>
                        <td width="100">
                            <asp:DropDownList ID="ddlLogType" runat="server" AutoPostBack="true">
                            </asp:DropDownList>
                        </td>
                    </tr>
                    <tr>
                        <td class="SubHead" nowrap align="left">
                            <dnn:Label ID="plRecordsPage" runat="server" CssClass="SubHead" ResourceKey="Recordsperpage"
                                Suffix=":"></dnn:Label>
                        </td>
                        <td width="25">
                            <asp:DropDownList ID="ddlRecordsPerPage" runat="server" AutoPostBack="True">
                                <asp:ListItem Value="10">10</asp:ListItem>
                                <asp:ListItem Value="25">25</asp:ListItem>
                                <asp:ListItem Value="50">50</asp:ListItem>
                                <asp:ListItem Value="100">100</asp:ListItem>
                                <asp:ListItem Value="250">250</asp:ListItem>
                            </asp:DropDownList>
                        </td>
                        <td width="25">
                            &nbsp;
                        </td>
                        <td class="SubHead" colspan="2">
                            <asp:CheckBox ID="chkColorCoding" runat="server" resourcekey="ColorCoding" Text="Color Coding On"
                                AutoPostBack="true"></asp:CheckBox>
                        </td>
                        <td width="*">
                            &nbsp;
                        </td>
                    </tr>
                    <tr>
                        <td colspan="5">
                            <table id="tblInstructions" width="100%" border="0" runat="server">
                                <tr height="10">
                                    <td>
                                    </td>
                                </tr>
                                <tr>
                                    <td class="Normal" align="center">
                                        <asp:Label ID="lbClickRow" runat="server" resourcekey="ClickRow">Click on a row for details.</asp:Label><br>
                                    </td>
                                </tr>
                                <tr>
                                    <td class="NormalBold" align="center">
                                        <asp:Label ID="txtShowing" runat="server"></asp:Label>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </td>
            <td width="*">
                &nbsp;
            </td>
            <td class="SubHead" valign="top" align="left" width="230" rowspan="3">
                <asp:Panel ID="pnlLegend" runat="server" HorizontalAlign="Center">
                    <dnn:SectionHead ID="dshLegend" runat="server" CssClass="Head" ResourceKey="Legend"
                        Section="tblLegend" Text="Color Coding Legend" IsExpanded="False"></dnn:SectionHead>
                    <table id="tblLegend" cellspacing="2" cellpadding="2" bgcolor="#000000" border="0"
                        runat="server">
                        <tr>
                            <td bgcolor="#ffffff">
                                <table border="0">
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#ff1414">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label1" runat="server" resourcekey="ExceptionCode">Exception</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#009900">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label2" runat="server" resourcekey="ItemCreatedCode">Item Created</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#009999">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label3" runat="server" resourcekey="ItemUpdatedCode">Item Updated</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#14ffff">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label4" runat="server" resourcekey="ItemDeletedCode">Item Deleted</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#999900">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal" nowrap>
                                            <asp:Label ID="Label5" runat="server" resourcekey="SuccessCode">Operation Success</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#990000">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label6" runat="server" resourcekey="FailureCode">Operation Failure</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#4d0099">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label7" runat="server" resourcekey="AdminOpCode">General Admin Operation</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#148aff">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label8" runat="server" resourcekey="AdminAlertCode">Admin Alert</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#ff8a14">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label9" runat="server" resourcekey="HostAlertCode">Host Alert</asp:Label>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>
                                            <table cellspacing="1" cellpadding="3" bgcolor="#000000" border="0">
                                                <tr>
                                                    <td class="Normal" bgcolor="#000000">
                                                        <img height="5" src="images/spacer.gif" width="5">
                                                    </td>
                                                </tr>
                                            </table>
                                        </td>
                                        <td class="Normal">
                                            <asp:Label ID="Label10" runat="server" resourcekey="SecurityException">Security Exception</asp:Label>
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </asp:Panel>
            </td>
        </tr>
    </table>
    <br>
</asp:Panel>
<br>
<asp:DataList EnableViewState="False" Width="100%" runat="server" ID="dlLog" CellPadding="0" CellSpacing="1" BackColor="#CFCFCF">
    <ItemStyle BorderWidth="0" />
    <HeaderStyle BackColor="#CFCFCF" BorderWidth="0" />
    <HeaderTemplate>
        <span class="NormalBold" style="width: 20; background: #CFCFCF;">&nbsp;</span>
        <asp:Label ID="lblDateHeader" runat="server" resourcekey="Date" class="NormalBold"
            Style="width: 150; background: #CFCFCF;">&nbsp;Date</asp:Label>
        <asp:Label ID="lblTypeHeader" runat="server" resourcekey="Type" class="NormalBold"
            Style="width: 200; background: #CFCFCF;">&nbsp;Log Type</asp:Label>
        <asp:Label ID="lblUserHeader" runat="server" resourcekey="Username" class="NormalBold"
            Style="width: 100; background: #CFCFCF;">&nbsp;Username</asp:Label>
        <asp:Label ID="lblPortalHeader" runat="server" resourcekey="Portal" class="NormalBold"
            Style="width: 150; background: #CFCFCF;">&nbsp;Portal</asp:Label>
        <asp:Label ID="lblSummaryHeader" runat="server" resourcekey="Summary" class="NormalBold"
            Style="width: 270; background: #CFCFCF;">&nbsp;Summary</asp:Label>
    </HeaderTemplate>
    <ItemTemplate>
        <table width="100%" border="0" cellspacing="0" cellpadding="0">
            <tr class='<%# GetMyLogType(DataBinder.Eval(Container.DataItem,"LogTypeKey")).LogTypeCSSclass %>'>
                <td width="20" valign="middle" align="center">
                    <input type="checkbox" name="Exception" value='<%# CType(Container.DataItem, DotNetNuke.Services.Log.EventLog.LogInfo).LogGUid %>|<%# CType(Container.DataItem, DotNetNuke.Services.Log.EventLog.LogInfo).LogFileid %>' />
                </td>
                <td nowrap="nowrap" onclick="flipFlop('<%# CType(Container.DataItem, DotNetNuke.Services.Log.EventLog.LogInfo).LogGUid %>')">
                    <span class="Normal" style="width: 150; overflow: hidden;">&nbsp;
                        <asp:Label EnableViewState="False" runat="server" Text='<%# DataBinder.Eval(Container.DataItem,"LogCreateDate") %>'
                            ID="lblDate" /></span> <span class="Normal" style="width: 200; overflow: hidden;">&nbsp;
                                <asp:Label EnableViewState="False" runat="server" Text='<%# GetMyLogType(DataBinder.Eval(Container.DataItem,"LogTypeKey")).LogTypeFriendlyName %>'
                                    ID="lblType" /></span> <span class="Normal" style="width: 100; overflow: hidden;">&nbsp;
                                        <asp:Label EnableViewState="False" runat="server" Text='<%# DataBinder.Eval(Container.DataItem,"LogUserName") %>'
                                            ID="lblUserName" /></span> <span class="Normal" style="width: 150; overflow: hidden;">
                                                &nbsp;
                                                <asp:Label EnableViewState="False" runat="server" Text='<%# DataBinder.Eval(Container.DataItem,"LogPortalName") %>'
                                                    ID="lblPortal" /></span> <span class="Normal" style="width: 280; overflow: hidden;">
                                                        &nbsp;
                                                        <asp:Label EnableViewState="False" runat="server" Text='<%# CType(Container.DataItem, DotNetNuke.Services.Log.EventLog.LogInfo).LogProperties.Summary %>'
                                                            ID="lblSummary" />&nbsp;...</span>
                </td>
            </tr>
            <tr style="display: none;" id='<%# CType(Container.DataItem, DotNetNuke.Services.Log.EventLog.LogInfo).LogGUid %>'>
                <td colspan="2" nowrap bgcolor="#FFFFFF">
                    <table width="100%" border="0" bgcolor="#000000" border="0" cellspacing="1" cellpadding="5">
                        <tr>
                            <td bgcolor="#CFCFCF">
                                <asp:Label class="Normal" EnableViewState="False" runat="server" Text='<%# GetPropertiesText(Container.DataItem) %>'
                                    ID="lblException" />
                            </td>
                        </tr>
                    </table>
                </td>
            </tr>
        </table>
    </ItemTemplate>
</asp:DataList>
<dnnsc:PagingControl ID="ctlPagingControlBottom" runat="server"></dnnsc:PagingControl>
<p>
    <asp:LinkButton CssClass="CommandButton" ID="btnDelete" resourcekey="btnDelete" runat="server">Delete Selected Exceptions</asp:LinkButton>
    &nbsp;&nbsp;
    <asp:LinkButton CssClass="CommandButton" ID="btnClear" resourcekey="btnClear" runat="server">Clear Log</asp:LinkButton>
</p>
<asp:Panel ID="pnlSendExceptions" runat="server" CssClass="Normal">
    <dnn:SectionHead ID="dshSendExceptions" runat="server" CssClass="Head" ResourceKey="SendExceptions"
        Section="tblSendExceptions" Text="Send Exceptions" IsExpanded="False" IncludeRule="True">
    </dnn:SectionHead>
    <table id="tblSendExceptions" cellspacing="2" cellpadding="2" width="560" border="0"
        runat="server">
        <tr>
            <td colspan="3">
                <asp:Label class="normal" ID="lblExceptionsWarning" runat="server" resourcekey="ExceptionsWarning">
          <B>Please note</B>: By using these features 
            below, you <I>may</I> be sending sensitive data over the Internet in clear 
            text (<I>not</I> encrypted). Before sending your exception submission, 
            please review the contents of your exception log to verify that no 
            sensitive data is contained within it. Only the log entries checked above 
            will be sent.
                </asp:Label>
                <hr noshade size="1">
                <asp:RadioButtonList ID="optEmailType" runat="server" CssClass="Normal" AutoPostBack="False"
                    RepeatDirection="Horizontal">
                    <asp:ListItem Value="Email" Selected="True" resourcekey="ToEmail.Text">To Specified Email Address</asp:ListItem>
                </asp:RadioButtonList>
            </td>
        </tr>
        <tr>
            <td class="SubHead" align="left" width="200">
                <dnn:Label ID="plEmailAddress" runat="server" ControlName="txtEmailAddress" Suffix=":">
                </dnn:Label>
            </td>
            <td width="200">
                <asp:TextBox ID="txtEmailAddress" runat="server"></asp:TextBox>
            </td>
            <td width="*">
                &nbsp;
            </td>
        </tr>
        <tr>
            <td class="SubHead" align="left" width="200">
                <dnn:Label ID="plMessage" runat="server" ResourceKey="SendMessage" ControlName="txtMessage"
                    Suffix=":"></dnn:Label>
            </td>
            <td width="200">
                <asp:TextBox ID="txtMessage" runat="server" Rows="6" Columns="25" TextMode="MultiLine"></asp:TextBox>
            </td>
            <td width="*">
                &nbsp;
            </td>
        </tr>
        <tr>
            <td colspan="3">
                <asp:LinkButton ID="btnEmail" runat="server" CssClass="CommandButton" resourcekey="btnEmail">Send Selected Exceptions</asp:LinkButton>
            </td>
        </tr>
    </table>
</asp:Panel>
