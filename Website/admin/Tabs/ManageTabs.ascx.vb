'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'

Imports System.Collections.Generic
Imports System.IO
Imports System.Xml
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Definitions
Imports DotNetNuke.UI.Utilities
Imports DotNetNuke.Services.FileSystem

Namespace DotNetNuke.Modules.Admin.Tabs

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The ManageTabs PortalModuleBase is used to manage a Tab/Page
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
    '''                       and localisation
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Partial Class ManageTabs
        Inherits DotNetNuke.Entities.Modules.PortalModuleBase

#Region "Private Members"

        Private strAction As String = ""

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' BindData loads the Controls with Tab Data from the Database
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub BindData()

            Dim objTabs As New TabController
            Dim objTab As TabInfo = objTabs.GetTab(TabId, PortalId, False)

            'Load TabControls
            LoadTabControls(objTab)

            If Not objTab Is Nothing Then

                If strAction <> "copy" Then
                    txtTabName.Text = objTab.TabName
                    txtTitle.Text = objTab.Title
                    txtDescription.Text = objTab.Description
                    txtKeyWords.Text = objTab.KeyWords
                    ctlURL.Url = objTab.Url
                End If
                ctlIcon.Url = objTab.IconFile
                If Not cboTab.Items.FindByValue(objTab.ParentId.ToString) Is Nothing Then
                    cboTab.Items.FindByValue(objTab.ParentId.ToString).Selected = True
                End If
                chkMenu.Checked = objTab.IsVisible

                If TabId <> PortalSettings.AdminTabId And TabId <> PortalSettings.SplashTabId And TabId <> PortalSettings.HomeTabId And TabId <> PortalSettings.LoginTabId And TabId <> PortalSettings.UserTabId Then
                    chkDisableLink.Checked = objTab.DisableLink
                Else
                    chkDisableLink.Enabled = False
                End If

                ctlSkin.SkinSrc = objTab.SkinSrc
                ctlContainer.SkinSrc = objTab.ContainerSrc

                If Entities.Portals.PortalSettings.GetSiteSetting(PortalId, "SSLEnabled") = "True" Then
                    chkSecure.Enabled = True
                    chkSecure.Checked = objTab.IsSecure
                Else
                    chkSecure.Enabled = False
                    chkSecure.Checked = False
                End If
                If Not Null.IsNull(objTab.StartDate) Then
                    txtStartDate.Text = objTab.StartDate.ToShortDateString
                End If
                If Not Null.IsNull(objTab.EndDate) Then
                    txtEndDate.Text = objTab.EndDate.ToShortDateString
                End If
                If objTab.RefreshInterval <> Null.NullInteger Then
                    txtRefreshInterval.Text = objTab.RefreshInterval.ToString
                End If

                txtPageHeadText.Text = objTab.PageHeadText
            End If

            ' copy page options
            cboCopyPage.DataSource = LoadPortalTabs()
            cboCopyPage.DataBind()
            cboCopyPage.Items.Insert(0, New ListItem("<" + Services.Localization.Localization.GetString("None_Specified") + ">", "-1"))
            rowModules.Visible = False
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CheckQuota checks whether the Page Quota will be exceeded
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/16/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub CheckQuota()

            If PortalSettings.Pages < PortalSettings.PageQuota Or UserInfo.IsSuperUser Or PortalSettings.PageQuota = 0 Then
                cmdUpdate.Enabled = True
            Else
                cmdUpdate.Enabled = False
                DotNetNuke.UI.Skins.Skin.AddModuleMessage(Me, Localization.GetString("ExceededQuota", Me.LocalResourceFile), Skins.Controls.ModuleMessage.ModuleMessageType.YellowWarning)
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' InitializeTab loads the Controls with default Tab Data
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub InitializeTab()

            'Load TabControls
            LoadTabControls(Nothing)

            ' Populate Tab Names, etc.
            txtTabName.Text = ""
            txtTitle.Text = ""
            txtDescription.Text = ""
            txtKeyWords.Text = ""
            chkMenu.Checked = True

            ' tab administrators can only create children of the current tab
            If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = False Then
                If Not cboTab.Items.FindByValue(TabId.ToString) Is Nothing Then
                    cboTab.Items.FindByValue(TabId.ToString).Selected = True
                End If
            Else
                ' select the <None Specified> option
                cboTab.Items(0).Selected = True
            End If

            ' hide the upload new file link until the tab has been saved
            chkDisableLink.Checked = False

            cboFolders.Items.Insert(0, New ListItem("<" + Services.Localization.Localization.GetString("None_Specified") + ">", "-"))
            Dim folders As ArrayList = FileSystemUtils.GetFoldersByUser(PortalId, False, False, "READ, WRITE")
            For Each folder As FolderInfo In folders
                Dim FolderItem As New ListItem
                If folder.FolderPath = Null.NullString Then
                    FolderItem.Text = Localization.GetString("Root", Me.LocalResourceFile)
                Else
                    FolderItem.Text = folder.FolderPath
                End If
                FolderItem.Value = folder.FolderPath
                cboFolders.Items.Add(FolderItem)
                If FolderItem.Value = "Templates/" Then
                    FolderItem.Selected = True
                    LoadTemplates()
                End If
            Next

            ' copy page options
            cboCopyPage.DataSource = LoadPortalTabs()
            cboCopyPage.DataBind()
            cboCopyPage.Items.Insert(0, New ListItem("<" + Services.Localization.Localization.GetString("None_Specified") + ">", "-1"))
            rowModules.Visible = False

        End Sub

        Private Sub LoadTemplates()
            cboTemplate.Items.Clear()
            If cboFolders.SelectedIndex <> 0 Then
                Dim arrFiles As ArrayList = Common.Globals.GetFileList(PortalId, "page.template", False, cboFolders.SelectedItem.Value)
                Dim objFile As FileItem
                For Each objFile In arrFiles
                    Dim FileItem As New ListItem
                    FileItem.Text = objFile.Text.Replace(".page.template", "")
                    FileItem.Value = objFile.Text
                    cboTemplate.Items.Add(FileItem)
                    If Not Page.IsPostBack AndAlso FileItem.Text = "Default" Then
                        FileItem.Selected = True
                    End If
                Next
                cboTemplate.Items.Insert(0, New ListItem(Localization.GetString("None_Specified"), "-1"))
                If cboTemplate.SelectedIndex = -1 Then
                    cboTemplate.SelectedIndex = 0
                End If
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' SaveTabData saves the Tab to the Database
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="strAction">The action to perform "edit" or "add"</param>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function SaveTabData(ByVal strAction As String) As Integer

            Dim objEventLog As New Services.Log.EventLog.EventLogController

            Dim strIcon As String = ""
            strIcon = ctlIcon.Url

            Dim objTabs As New TabController

            Dim objTab As New TabInfo

            objTab.TabID = TabId
            objTab.PortalID = PortalId
            objTab.TabName = txtTabName.Text
            objTab.Title = txtTitle.Text
            objTab.Description = txtDescription.Text
            objTab.KeyWords = txtKeyWords.Text
            objTab.IsVisible = chkMenu.Checked
            If TabId <> PortalSettings.AdminTabId And TabId <> PortalSettings.SplashTabId And TabId <> PortalSettings.HomeTabId And TabId <> PortalSettings.LoginTabId And TabId <> PortalSettings.UserTabId Then
                objTab.DisableLink = chkDisableLink.Checked
            End If

            objTab.ParentId = Int32.Parse(cboTab.SelectedItem.Value)
            objTab.IconFile = strIcon
            objTab.IsDeleted = False
            objTab.Url = ctlURL.Url
            objTab.TabPermissions = dgPermissions.Permissions
            objTab.SkinSrc = ctlSkin.SkinSrc
            objTab.ContainerSrc = ctlContainer.SkinSrc
            objTab.TabPath = GenerateTabPath(objTab.ParentId, objTab.TabName)

            'Check for invalid 
            If Regex.IsMatch(objTab.TabName, "^AUX$|^CON$|^LPT[1-9]$|^CON$|^COM[1-9]$|^NUL$", RegexOptions.IgnoreCase) Then
                Skins.Skin.AddModuleMessage(Me, Localization.GetString("InvalidTabName", Me.LocalResourceFile), Skins.Controls.ModuleMessage.ModuleMessageType.RedError)
                Return Null.NullInteger
            End If


            'Validate Tab Path
            If String.IsNullOrEmpty(strAction) Then
                Dim tabID As Integer = TabController.GetTabByTabPath(objTab.PortalID, objTab.TabPath)

                If tabID <> Null.NullInteger Then
                    Skin.AddModuleMessage(Me, Localization.GetString("TabExists", Me.LocalResourceFile), Skins.Controls.ModuleMessage.ModuleMessageType.RedError)
                    Return Null.NullInteger
                End If
            End If

            If txtStartDate.Text <> "" Then
                objTab.StartDate = Convert.ToDateTime(txtStartDate.Text)
            Else
                objTab.StartDate = Null.NullDate
            End If
            If txtEndDate.Text <> "" Then
                objTab.EndDate = Convert.ToDateTime(txtEndDate.Text)
            Else
                objTab.EndDate = Null.NullDate
            End If
            If txtRefreshInterval.Text.Length > 0 AndAlso IsNumeric(txtRefreshInterval.Text) Then
                objTab.RefreshInterval = Convert.ToInt32(txtRefreshInterval.Text)
            End If
            objTab.PageHeadText = txtPageHeadText.Text
            objTab.IsSecure = chkSecure.Checked

            If strAction = "edit" Then

                ' trap circular tab reference
                If objTab.TabID <> Int32.Parse(cboTab.SelectedItem.Value) And Not IsCircularReference(Int32.Parse(cboTab.SelectedItem.Value)) Then
                    objTabs.UpdateTab(objTab)
                    objEventLog.AddLog(objTab, PortalSettings, UserId, "", Services.Log.EventLog.EventLogController.EventLogType.TAB_UPDATED)
                End If

            Else ' add or copy

                objTab.TabID = objTabs.AddTab(objTab)
                objEventLog.AddLog(objTab, PortalSettings, UserId, "", Services.Log.EventLog.EventLogController.EventLogType.TAB_CREATED)

                Dim copyTabId As Integer = Int32.Parse(cboCopyPage.SelectedItem.Value)
                If copyTabId <> -1 Then
                    Dim objDataGridItem As DataGridItem
                    Dim objModules As New ModuleController
                    Dim objModule As ModuleInfo
                    Dim chkModule As CheckBox
                    Dim optNew As RadioButton
                    Dim optCopy As RadioButton
                    Dim optReference As RadioButton
                    Dim txtCopyTitle As TextBox

                    For Each objDataGridItem In grdModules.Items
                        chkModule = CType(objDataGridItem.FindControl("chkModule"), CheckBox)
                        If chkModule.Checked Then
                            Dim intModuleID As Integer = CType(grdModules.DataKeys(objDataGridItem.ItemIndex), Integer)
                            optNew = CType(objDataGridItem.FindControl("optNew"), RadioButton)
                            optCopy = CType(objDataGridItem.FindControl("optCopy"), RadioButton)
                            optReference = CType(objDataGridItem.FindControl("optReference"), RadioButton)
                            txtCopyTitle = CType(objDataGridItem.FindControl("txtCopyTitle"), TextBox)

                            objModule = objModules.GetModule(intModuleID, copyTabId, False)
                            If Not objModule Is Nothing Then
                                'Clone module as it exists in the cache and changes we make will update the cached object
                                Dim newModule As ModuleInfo = objModule.Clone()

                                If Not optReference.Checked Then
                                    newModule.ModuleID = Null.NullInteger
                                End If

                                newModule.TabID = objTab.TabID
                                newModule.ModuleTitle = txtCopyTitle.Text
                                newModule.ModuleID = objModules.AddModule(newModule)

                                If optCopy.Checked Then
                                    If newModule.BusinessControllerClass <> "" Then
                                        Dim objObject As Object = Framework.Reflection.CreateObject(newModule.BusinessControllerClass, newModule.BusinessControllerClass)
                                        If TypeOf objObject Is IPortable Then
                                            Try
                                                Dim Content As String = CType(CType(objObject, IPortable).ExportModule(intModuleID), String)
                                                If Content <> "" Then
                                                    CType(objObject, IPortable).ImportModule(newModule.ModuleID, Content, newModule.Version, UserInfo.UserID)
                                                End If
                                            Catch exc As Exception
                                                ' the export/import operation failed
                                                ProcessModuleLoadException(Me, exc)
                                            End Try
                                        End If
                                    End If
                                End If
                            End If
                        End If
                    Next
                Else
                    ' create the page from a template
                    If Not cboTemplate.SelectedItem Is Nothing Then
                        If cboTemplate.SelectedItem.Value <> "" Then
                            ' open the XML file
                            Try
                                Dim xmlDoc As New XmlDocument
                                xmlDoc.Load(PortalSettings.HomeDirectoryMapPath & cboFolders.SelectedValue & cboTemplate.SelectedValue)
                                TabController.DeserializePanes(xmlDoc.SelectSingleNode("//portal/tabs/tab/panes"), objTab.PortalID, objTab.TabID, PortalTemplateModuleAction.Ignore, New Hashtable)
                            Catch
                                ' error opening page template
                            End Try
                        End If
                    End If
                End If
            End If

            ' url tracking
            Dim objUrls As New UrlController
            objUrls.UpdateUrl(PortalId, ctlURL.Url, ctlURL.UrlType, 0, Null.NullDate, Null.NullDate, ctlURL.Log, ctlURL.Track, Null.NullInteger, ctlURL.NewWindow)

            'Clear the Tab's Cached modules
            DotNetNuke.Common.Utilities.DataCache.ClearModuleCache(TabId)

            'Update DesktopTabs as TabPath may be needed before cache is cleared
            For Each Tab As TabInfo In PortalSettings.DesktopTabs
                If Tab.TabID = objTab.TabID Then
                    Tab.TabPath = objTab.TabPath
                End If
            Next

            Return objTab.TabID

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Checks if parent tab will cause a circular reference
        ''' </summary>
        ''' <param name="intTabId">Tabid</param>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	28/11/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function IsCircularReference(ByVal intTabId As Integer) As Boolean
            If intTabId <> -1 Then
                Dim objTabs As New TabController
                Dim objtab As TabInfo = objTabs.GetTab(intTabId, PortalId, False)

                If objtab.Level = 0 Then
                    Return False
                Else
                    If TabId = objtab.ParentId Then
                        Return True
                    Else
                        Return IsCircularReference(objtab.ParentId)
                    End If
                End If
            Else
                Return False
            End If

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes Tab
        ''' </summary>
        ''' <param name="Tabid">ID of the parent tab</param>
        ''' <remarks>
        ''' Will delete tab
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	30/09/2004	Created
        '''     [VMasanas]  01/09/2005  A tab will be deleted only if all descendants can be deleted
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function DeleteTab(ByVal deleteTabId As Integer) As Boolean

            Dim bDeleted As Boolean = TabController.DeleteTab(deleteTabId, PortalSettings, UserId)

            If Not bDeleted Then
                UI.Skins.Skin.AddModuleMessage(Me, Services.Localization.Localization.GetString("DeleteSpecialPage", Me.LocalResourceFile), UI.Skins.Controls.ModuleMessage.ModuleMessageType.RedError)
            End If

            Return bDeleted

        End Function

        Private Sub LoadTabControls(ByVal objTab As TabInfo)

            Dim currentTabId As Integer

            If objTab Is Nothing Then
                currentTabId = -1
            Else
                currentTabId = objTab.TabID
            End If
            Dim arr As ArrayList
            arr = GetPortalTabs(PortalSettings.DesktopTabs, currentTabId, True, True, False, True, True)
            cboTab.DataSource = arr
            cboTab.DataBind()
            ' if editing a tab, load tab parent so parent link is not lost
            ' parent tab might not be loaded in cbotab if user does not have edit rights on it
            If Not PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) And Not objTab Is Nothing Then
                If cboTab.Items.FindByValue(objTab.ParentId.ToString) Is Nothing Then
                    Dim objtabs As New TabController
                    Dim objparent As TabInfo = objtabs.GetTab(objTab.ParentId, objTab.PortalID, False)
                    cboTab.Items.Add(New ListItem(objparent.TabName, objparent.TabID.ToString))
                End If
            End If
        End Sub

        Private Function LoadPortalTabs() As ArrayList

            Dim arrTabs As New ArrayList

            Dim objTab As TabInfo
            Dim arrPortalTabs As ArrayList = GetPortalTabs(PortalSettings.DesktopTabs, False, True)
            For Each objTab In arrPortalTabs
                If PortalSecurity.IsInRoles(objTab.AuthorizedRoles) Then
                    arrTabs.Add(objTab)
                End If
            Next

            Return arrTabs

        End Function

        Private Function LoadTabModules(ByVal TabID As Integer) As ArrayList

            Dim objModules As New ModuleController
            Dim arrModules As New ArrayList

            For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In objModules.GetTabModules(TabID)
                Dim objModule As ModuleInfo = kvp.Value

                If PortalSecurity.IsInRoles(objModule.AuthorizedEditRoles) = True And objModule.IsDeleted = False And objModule.AllTabs = False Then
                    arrModules.Add(objModule)
                End If
            Next

            Return arrModules

        End Function

        Private Sub DisplayTabModules()
            Select Case cboCopyPage.SelectedIndex
                Case 0
                    rowModules.Visible = False
                Case Else ' selected tab
                    grdModules.DataSource = LoadTabModules(Integer.Parse(cboCopyPage.SelectedItem.Value))
                    grdModules.DataBind()
                    rowModules.Visible = True
            End Select
        End Sub

        ''' <summary>
        ''' Gets all children tabs where user has edit access
        ''' </summary>
        ''' <returns>All the childen tabs where current user has edit permission</returns>
        ''' <remarks>
        ''' To get desired tabs it first selects children tabs (by using the taborder and level) 
        ''' and then filters only those where the user has access
        ''' </remarks>
        Private Function GetEditableTabs() As ArrayList
            Dim arr As New ArrayList
            Dim i As Integer = 0
            Dim Finished As Boolean = False

            While i < PortalSettings.DesktopTabs.Count And Not Finished
                If PortalSettings.DesktopTabs(i).TabOrder > PortalSettings.ActiveTab.TabOrder Then
                    If PortalSettings.DesktopTabs(i).level > PortalSettings.ActiveTab.Level Then
                        ' we are in a descendant
                        arr.Add(PortalSettings.DesktopTabs(i))
                    Else
                        ' exit condition
                        Finished = True
                    End If
                End If
                i += 1
            End While

            If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) Then
                ' shortcut for admins
                Return arr
            Else
                ' filter tabs where user has access
                Return GetPortalTabs(arr, PortalSettings.ActiveTab.TabID, False, True, False, True, True)
            End If

        End Function

#End Region

#Region "EventHandlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Page_Load runs when the control is loaded
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        '''     [VMasanas]  9/28/2004   Changed redirect to Access Denied
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
            Try
                Dim objModules As New ModuleController

                ' Verify that the current user has access to edit this module
                If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = False And PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString) = False Then
                    Response.Redirect(NavigateURL("Access Denied"), True)
                End If

                If Not (Request.QueryString("action") Is Nothing) Then
                    strAction = Request.QueryString("action").ToLower
                End If

                'this needs to execute always to the client script code is registred in InvokePopupCal
                cmdStartCalendar.NavigateUrl = Common.Utilities.Calendar.InvokePopupCal(txtStartDate)
                cmdEndCalendar.NavigateUrl = Common.Utilities.Calendar.InvokePopupCal(txtEndDate)

                If Page.IsPostBack = False Then
                    'Set the tab id of the permissions grid to the TabId (Note If in add mode
                    'this means that the default permissions inherit from the parent)
                    If strAction = "edit" Or strAction = "delete" Or PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = False Then
                        dgPermissions.TabID = TabId
                    Else
                        dgPermissions.TabID = -1
                    End If

                    ClientAPI.AddButtonConfirm(cmdDelete, Services.Localization.Localization.GetString("DeleteItem"))

                    ' load the list of files found in the upload directory
                    ctlIcon.ShowFiles = True
                    ctlIcon.ShowTabs = False
                    ctlIcon.ShowUrls = False
                    ctlIcon.Required = False

                    ctlIcon.ShowLog = False
                    ctlIcon.ShowNewWindow = False
                    ctlIcon.ShowTrack = False
                    ctlIcon.FileFilter = glbImageFileTypes
                    ctlIcon.Width = "275px"

                    ' tab administrators can only manage their own tab
                    If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = False Then
                        cboTab.Enabled = False
                    End If

                    ctlSkin.Width = "275px"
                    ctlSkin.SkinRoot = UI.Skins.SkinInfo.RootSkin
                    ctlContainer.Width = "275px"
                    ctlContainer.SkinRoot = UI.Skins.SkinInfo.RootContainer

                    ctlURL.Width = "275px"

                    rowCopySkin.Visible = False
                    rowCopyPerm.Visible = False
                    Select Case strAction
                        Case ""       ' add
                            CheckQuota()
                            InitializeTab()
                            rowTemplate1.Visible = True
                            rowTemplate2.Visible = True
                            cboCopyPage.SelectedIndex = 0
                            cmdDelete.Visible = False
                        Case "edit"
                            BindData()
                            rowCopyPerm.Visible = True
                            rowCopySkin.Visible = True
                            dshCopy.Visible = False
                            tblCopy.Visible = False
                        Case "copy"
                            CheckQuota()
                            BindData()
                            If Not cboCopyPage.Items.FindByValue(TabId.ToString) Is Nothing Then
                                cboCopyPage.Items.FindByValue(TabId.ToString).Selected = True
                                DisplayTabModules()
                            End If
                            cmdDelete.Visible = False
                        Case "delete"
                            If DeleteTab(TabId) Then
                                Response.Redirect(AddHTTP(PortalAlias.HTTPAlias), True)
                            Else
                                strAction = "edit"
                                BindData()
                                dshCopy.Visible = False
                                tblCopy.Visible = False
                            End If
                    End Select

                End If

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' cmdCancel_Click runs when the Cancel Button is clicked
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub cmdCancel_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles cmdCancel.Click
            Try

                Dim strURL As String = NavigateURL()

                If Not Request.QueryString("returntabid") Is Nothing Then
                    ' return to admin tab
                    strURL = NavigateURL(Convert.ToInt32(Request.QueryString("returntabid")))
                End If

                Response.Redirect(strURL, True)

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' cmdUpdate_Click runs when the Update Button is clicked
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub cmdUpdate_Click(ByVal Sender As Object, ByVal e As EventArgs) Handles cmdUpdate.Click
            Try
                If Page.IsValid Then
                    Dim intTabId As Integer = SaveTabData(strAction)

                    Dim strURL As String = NavigateURL(intTabId)

                    If Not Request.QueryString("returntabid") Is Nothing Then
                        ' return to admin tab
                        strURL = NavigateURL(Convert.ToInt32(Request.QueryString("returntabid").ToString))
                    Else
                        If ctlURL.Url <> "" Or chkDisableLink.Checked Then
                            ' redirect to current tab if URL was specified ( add or copy )
                            strURL = NavigateURL(TabId)
                        End If
                    End If

                    Response.Redirect(strURL, True)

                End If
            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' cmdDelete_Click runs when the Delete Button is clicked
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/10/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        '''     [VMasanas]  30/09/2004  When a parent tab is deleted all child are also marked as deleted.
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub cmdDelete_Click(ByVal Sender As Object, ByVal e As EventArgs) Handles cmdDelete.Click
            Try

                If DeleteTab(TabId) Then

                    Dim strURL As String = GetPortalDomainName(PortalAlias.HTTPAlias, Request)

                    If Not Request.QueryString("returntabid") Is Nothing Then
                        ' return to admin tab
                        strURL = NavigateURL(Convert.ToInt32(Request.QueryString("returntabid").ToString))
                    End If

                    Response.Redirect(strURL, True)

                End If

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Sub cboFolders_SelectedIndexChanged(ByVal sender As Object, ByVal e As System.EventArgs) Handles cboFolders.SelectedIndexChanged
            LoadTemplates()
        End Sub

        Private Sub cboCopyPage_SelectedIndexChanged(ByVal sender As Object, ByVal e As System.EventArgs) Handles cboCopyPage.SelectedIndexChanged
            DisplayTabModules()
        End Sub

        Protected Sub cmdCopySkin_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles cmdCopySkin.Click
            Try
                Dim objtabs As New TabController
                Dim arr As ArrayList = GetEditableTabs()

                objtabs.CopyDesignToChildren(arr, ctlSkin.SkinSrc, ctlContainer.SkinSrc)
            Catch ex As Exception
                ProcessModuleLoadException(Me, ex)
            End Try
        End Sub

        Protected Sub cmdCopyPerm_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles cmdCopyPerm.Click
            Try
                Dim objtabs As New TabController
                Dim arr As ArrayList = GetEditableTabs()

                objtabs.CopyPermissionsToChildren(arr, dgPermissions.Permissions)
            Catch ex As Exception
                ProcessModuleLoadException(Me, ex)
            End Try
        End Sub

#End Region

    End Class

End Namespace
