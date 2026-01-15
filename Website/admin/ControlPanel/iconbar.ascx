<%@ Control language="vb" AutoEventWireup="false" Explicit="True" Inherits="DotNetNuke.UI.ControlPanels.IconBar" CodeFile="IconBar.ascx.vb" %>
<table class="ControlPanel" cellspacing="0" cellpadding="0" border="0">
	<tr>
		<td>
			<table cellspacing="1" cellpadding="1" style="width:100%;">
				<tr>
					<td style="text-align:left; vertical-align:middle; width:33%; white-space: nowrap;">
					    &nbsp;<asp:Label ID="lblMode" Runat="server" CssClass="SubHead" enableviewstate="False" />
						<asp:radiobuttonlist id="optMode" cssclass="SubHead" runat="server" repeatdirection="Horizontal" repeatlayout="Flow" autopostback="True">
							<asp:listitem value="VIEW" resourcekey="ModeView" />
							<asp:listitem value="EDIT" resourcekey="ModeEdit" />
							<asp:listitem value="DESIGN" resourcekey="ModeDesign" />
						</asp:radiobuttonlist>
					</td>
					<td style="text-align:center; vertical-align:middle; width:33%;"><asp:HyperLink ID="hypUpgrade" runat="server" Target="_new" Visible="False" /></td>
					<td style="text-align:right; vertical-align:middle; white-space: nowrap; width:33%;">
					    <asp:Label ID="lblVisibility" Runat="server" CssClass="SubHead" resourcekey="Visibility" />
                        <asp:LinkButton ID="cmdVisibility" Runat="server" CausesValidation="False"><asp:Image ID="imgVisibility" Runat="server" /></asp:LinkButton>&nbsp;
					</td>
				</tr>
			</table>
			<table cellspacing="1" cellpadding="1" style="width:100%;">
				<tr id="rowControlPanel" runat="server">
					<td style="border-top:1px #CCCCCC dotted; text-align:center; vertical-align:middle; width:25%;">
                        <asp:Label ID="lblPageFunctions" Runat="server" CssClass="SubHead" enableviewstate="False" />
						 <table  border="0" cellpadding="2" cellspacing="0" style="margin: 0 auto;">
							<tr style="height:24px; vertical-align:bottom">
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdAddTabIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgAddTabIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_addtab.gif" />
									</asp:LinkButton>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdEditTabIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgEditTabIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_edittab.gif" />
									</asp:LinkButton>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdDeleteTabIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgDeleteTabIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_deletetab.gif" />
									</asp:LinkButton>
								</td>
							</tr>
							<tr style="vertical-align:bottom">
								<td style="width:35px; text-align:center;" class="Normal"><asp:LinkButton ID="cmdAddTab" Runat="server" CssClass="CommandButton" CausesValidation="False" /></td>
								<td style="width:35px; text-align:center;" class="Normal"><asp:LinkButton ID="cmdEditTab" Runat="server" CssClass="CommandButton" CausesValidation="False" /></td>
								<td style="width:35px; text-align:center;" class="Normal"><asp:LinkButton ID="cmdDeleteTab" Runat="server" CssClass="CommandButton" CausesValidation="False" /></td>
							</tr>
							<tr style="height:24px; vertical-align:bottom">
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdCopyTabIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgCopyTabIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_copytab.gif" />
									</asp:LinkButton>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdExportTabIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgExportTabIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_exporttab.gif" />
									</asp:LinkButton>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdImportTabIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgImportTabIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_importtab.gif" />
									</asp:LinkButton>
								</td>
							</tr>
							<tr style="vertical-align:bottom">
								<td style="width:35px; text-align:center;" class="Normal"><asp:LinkButton ID="cmdCopyTab" Runat="server" CssClass="CommandButton" CausesValidation="False" /></td>
								<td style="width:35px; text-align:center;" class="Normal"><asp:LinkButton ID="cmdExportTab" Runat="server" CssClass="CommandButton" CausesValidation="False" /></td>
								<td style="width:35px; text-align:center;" class="Normal"><asp:LinkButton ID="cmdImportTab" Runat="server" CssClass="CommandButton" CausesValidation="False" /></td>
							</tr>
						</table>
					</td>
					<td style="border-left:1px #CCCCCC dotted; border-right:1px #CCCCCC dotted; border-top:1px #CCCCCC dotted; text-align:center; vertical-align:top; width:50%;">
						<asp:radiobuttonlist id="optModuleType" cssclass="SubHead" runat="server" repeatdirection="Horizontal" repeatlayout="Flow" autopostback="True">
							<asp:listitem value="0" resourcekey="optModuleTypeNew" />
							<asp:listitem value="1" resourcekey="optModuleTypeExisting" />
						</asp:radiobuttonlist>
						<table cellspacing="1" cellpadding="0" border="0">
							<tr>
								<td align="center">
									<table cellspacing="1" cellpadding="0" border="0">
							            <tr style="vertical-align:bottom">
											<td class="SubHead" style="text-align:right; white-space: nowrap;"><asp:Label ID="lblModule" Runat="server" CssClass="SubHead" enableviewstate="False" />&nbsp;</td>
											<td style="white-space: nowrap;">
											    <asp:dropdownlist id="cboTabs" runat="server" cssclass="NormalTextBox" Width="140" datavaluefield="TabID" datatextfield="TabName" visible="False" autopostback="True" />
												<asp:dropdownlist id="cboDesktopModules" runat="server" cssclass="NormalTextBox" Width="140" datavaluefield="DesktopModuleID" datatextfield="FriendlyName"/>&nbsp;&nbsp;
											</td>
											<td class="SubHead" style="text-align:right; white-space: nowrap;"><asp:Label ID="lblPane" Runat="server" CssClass="SubHead" enableviewstate="False" />&nbsp;</td>
											<td style="white-space: nowrap;"><asp:dropdownlist id="cboPanes" runat="server" cssclass="NormalTextBox" Width="110"/>&nbsp;&nbsp;</td>
											<td style="text-align:center; white-space: nowrap;">
											    <asp:LinkButton id="cmdAddModuleIcon" runat="server" cssclass="CommandButton" CausesValidation="False">
													<asp:Image runat="server" EnableViewState="False" ID="imgAddModuleIcon" ImageUrl="~/admin/ControlPanel/images/iconbar_addmodule.gif" />
												</asp:LinkButton>
											</td>
										</tr>
							            <tr style="vertical-align:bottom">
											<td class="SubHead"  style="text-align:right; white-space: nowrap;"><asp:Label ID="lblTitle" Runat="server" CssClass="SubHead" enableviewstate="False"/>&nbsp;</td>
											<td style="white-space: nowrap;">
											    <asp:dropdownlist id="cboModules" runat="server" cssclass="NormalTextBox" Width="140" datavaluefield="ModuleID" datatextfield="ModuleTitle" visible="False" />
											    <asp:TextBox ID="txtTitle" Runat="server" CssClass="NormalTextBox" Width="140" />&nbsp;&nbsp;
											</td>
											<td class="SubHead" style="text-align:right; white-space: nowrap;"><asp:Label ID="lblPosition" Runat="server" CssClass="SubHead" resourcekey="Position" enableviewstate="False" />&nbsp;</td>
											<td style="white-space: nowrap;">
												<asp:dropdownlist id="cboPosition" runat="server" CssClass="NormalTextBox" Width="110">
													<asp:ListItem Value="0" resourcekey="Top">Top</asp:ListItem>
													<asp:ListItem Value="-1" resourcekey="Bottom">Bottom</asp:ListItem>
												</asp:dropdownlist>&nbsp;&nbsp;
											</td>
											<td style="text-align:center; white-space: nowrap;" class="Normal"><asp:linkbutton id="cmdAddModule" runat="server" cssclass="CommandButton" CausesValidation="False" /></td>
										</tr>
							            <tr style="vertical-align:bottom">
											<td class="SubHead" style="text-align:right; white-space: nowrap;"><asp:Label ID="lblPermission" Runat="server" CssClass="SubHead" resourcekey="Permission" enableviewstate="False" />&nbsp;</td>
											<td style="white-space: nowrap;">
												<asp:dropdownlist id="cboPermission" runat="server" CssClass="NormalTextBox" Width="140">
													<asp:ListItem Value="0" resourcekey="PermissionView" />
													<asp:ListItem Value="1" resourcekey="PermissionEdit"/>
												</asp:dropdownlist>&nbsp;&nbsp;
											</td>
											<td class="SubHead" style="text-align:right; white-space: nowrap;"><asp:Label ID="lblAlign" Runat="server" CssClass="SubHead" enableviewstate="False" />&nbsp;</td>
											<td style="white-space: nowrap;">
												<asp:dropdownlist id="cboAlign" runat="server" CssClass="NormalTextBox" Width="110">
													<asp:ListItem Value="left" resourcekey="Left" />
													<asp:ListItem Value="center" resourcekey="Center" />
													<asp:ListItem Value="right" resourcekey="Right" />
										            <asp:listitem value="" resourcekey="Not_Specified" />
												</asp:dropdownlist>&nbsp;&nbsp;
											</td>
											<td style="text-align:center; white-space: nowrap;">&nbsp;</td>
										</tr>
									</table>
								</td>
							</tr>
						</table>
						<asp:linkbutton id="cmdInstallModules" runat="server" cssclass="CommandButton" CausesValidation="False" Visible="False" />
					</td>
					<td style="border-top:1px #CCCCCC dotted; text-align:center; vertical-align:middle; width:25%;">
                        <asp:Label ID="lblCommonTasks" Runat="server" CssClass="SubHead" enableviewstate="False"/>
						 <table  border="0" cellpadding="2" cellspacing="0" style="margin: 0 auto;">
							<tr style="height:24px; vertical-align:bottom">
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdSiteIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgSiteIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_site.gif" />
									</asp:LinkButton>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdUsersIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgUsersIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_users.gif"></asp:Image>
									</asp:LinkButton>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdRolesIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgRolesIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_roles.gif"></asp:Image>
									</asp:LinkButton>
								</td>
							</tr>
							<tr valign="bottom">
								<td style="width:35px;" align="center" class="Normal"><asp:LinkButton ID="cmdSite" Runat="server" CssClass="CommandButton" CausesValidation="False"/></td>
								<td style="width:35px;" align="center" class="Normal"><asp:LinkButton ID="cmdUsers" Runat="server" CssClass="CommandButton" CausesValidation="False"/></td>
								<td style="width:35px;" align="center" class="Normal"><asp:LinkButton ID="cmdRoles" Runat="server" CssClass="CommandButton" CausesValidation="False"/></td>
							</tr>
							<tr style="height:24px; vertical-align:bottom">
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdFilesIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgFilesIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_files.gif"></asp:Image>
									</asp:LinkButton>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:Hyperlink ID="cmdHelpIcon" Runat="server" CssClass="CommandButton" CausesValidation="False" Target="_new">
										<asp:Image ID="imgHelpIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_help.gif"></asp:Image>
									</asp:Hyperlink>
								</td>
								<td style="width:35px; text-align:center;">
								    <asp:LinkButton ID="cmdSolutionsIcon" Runat="server" CssClass="CommandButton" CausesValidation="False">
										<asp:Image ID="imgSolutionsIcon" Runat="server" ImageUrl="~/admin/ControlPanel/images/iconbar_solutions.gif"></asp:Image>
									</asp:LinkButton>
								</td>
							</tr>
							<tr valign="bottom">
								<td style="width:35px;" align="center" class="Normal"><asp:LinkButton ID="cmdFiles" Runat="server" CssClass="CommandButton" CausesValidation="False"/></td>
								<td style="width:35px;" align="center" class="Normal"><asp:Hyperlink ID="cmdHelp" Runat="server" CssClass="CommandButton" CausesValidation="False" Target="_new"/></td>
								<td style="width:35px;" align="center" class="Normal"><asp:LinkButton ID="cmdSolutions" Runat="server" CssClass="CommandButton" CausesValidation="False"/></td>
							</tr>
						</table>
					</td>
				</tr>
			</table>
		</td>
	</tr>
</table>
