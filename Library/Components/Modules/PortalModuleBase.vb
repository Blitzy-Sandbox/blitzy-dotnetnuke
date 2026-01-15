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

Imports System
Imports System.Configuration
Imports System.Data
Imports System.IO
Imports System.Threading

Imports DotNetNuke.Entities.Modules.Actions
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.UI.Skins

Namespace DotNetNuke.Entities.Modules

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Class	 : PortalModuleBase
    ''' 
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The PortalModuleBase class defines a custom base class inherited by all
    ''' desktop portal modules within the Portal.
    ''' 
    ''' The PortalModuleBase class defines portal specific properties
    ''' that are used by the portal framework to correctly display portal modules
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''		[cnurse]	09/17/2004	Added Documentation
    '''								Modified LocalResourceFile to be Writeable
    '''		[cnurse]	10/21/2004	Modified Settings property to get both
    '''								TabModuleSettings and ModuleSettings
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class PortalModuleBase
        Inherits UserControlBase

#Region "Private Members"

        Private _nextActionId As Integer = -1
        Private _isEditable As Integer = 0
        Private _localResourceFile As String
        Private _moduleConfiguration As ModuleInfo
        Private _settings As Hashtable
        Private _helpfile As String
        Private _helpurl As String
        Private _actions As ModuleActionCollection
        Private _cachedOutput As String = ""

        Private Shared objReaderWriterLock As New ReaderWriterLock()
        Private ReaderTimeOut As Integer = 10
        Private WriterTimeOut As Integer = 100

#End Region

#Region "Public Properties"

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Property Actions() As ModuleActionCollection
            Get
                If _actions Is Nothing Then
                    LoadActions()
                End If
                Return _actions
            End Get
            Set(ByVal Value As ModuleActionCollection)
                _actions = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CacheMethod property is used to store the Method used for this Module's
        ''' Cache
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 04/28/2005  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property CacheMethod() As String
            Get
                Return Convert.ToString(Common.Globals.HostSettings("ModuleCaching"))
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The EditMode property is used to determine whether the user is in the 
        ''' Administrator role
        ''' Cache
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 01/19/2006  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public ReadOnly Property EditMode() As Boolean
            Get
                Return PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) Or PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles.ToString)
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function EditUrl() As String
            Return EditUrl("", "", "Edit")
        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function EditUrl(ByVal ControlKey As String) As String
            Return EditUrl("", "", ControlKey)
        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function EditUrl(ByVal KeyName As String, ByVal KeyValue As String) As String
            Return EditUrl(KeyName, KeyValue, "Edit")
        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function EditUrl(ByVal KeyName As String, ByVal KeyValue As String, ByVal ControlKey As String) As String

            Dim key As String = ControlKey

            If (key = "") Then
                key = "Edit"
            End If

            If KeyName <> "" And KeyValue <> "" Then
                Return NavigateURL(PortalSettings.ActiveTab.TabID, key, "mid=" & ModuleId.ToString, KeyName & "=" & KeyValue)
            Else
                Return NavigateURL(PortalSettings.ActiveTab.TabID, key, "mid=" & ModuleId.ToString)
            End If

        End Function

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Function EditUrl(ByVal KeyName As String, ByVal KeyValue As String, ByVal ControlKey As String, ByVal ParamArray AdditionalParameters As String()) As String

            Dim key As String = ControlKey

            If (key = "") Then
                key = "Edit"
            End If

            If KeyName <> "" And KeyValue <> "" Then
                Dim params(AdditionalParameters.Length + 1) As String

                params(0) = "mid=" & ModuleId.ToString
                params(1) = KeyName & "=" & KeyValue

                For i As Integer = 0 To AdditionalParameters.Length - 1
                    params(i + 2) = AdditionalParameters(i)
                Next

                Return NavigateURL(PortalSettings.ActiveTab.TabID, key, params)
            Else
                Dim params(AdditionalParameters.Length) As String

                params(0) = "mid=" & ModuleId.ToString

                For i As Integer = 0 To AdditionalParameters.Length - 1
                    params(i + 1) = AdditionalParameters(i)
                Next

                Return NavigateURL(PortalSettings.ActiveTab.TabID, key, params)
            End If
        End Function

        <ObsoleteAttribute("The HelpFile() property was deprecated in version 2.2. Help files are now stored in the /App_LocalResources folder beneath the module with the following resource key naming convention: ModuleHelp.Text")> _
        Public Property HelpFile() As String
            Get
                Return _helpfile
            End Get
            Set(ByVal Value As String)
                _helpfile = Value
            End Set
        End Property

        Public Property HelpURL() As String
            Get
                Return _helpurl
            End Get
            Set(ByVal Value As String)
                _helpurl = Value
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property IsEditable() As Boolean
            Get

                ' Perform tri-state switch check to avoid having to perform a security
                ' role lookup on every property access (instead caching the result)
                If _isEditable = 0 Then

                    Dim blnPreview As Boolean = (PortalSettings.UserMode = PortalSettings.Mode.View)
                    If PortalSettings.ActiveTab.ParentId = PortalSettings.AdminTabId Or PortalSettings.ActiveTab.ParentId = PortalSettings.SuperTabId Then
                        blnPreview = False
                    End If

                    Dim blnHasModuleEditPermissions As Boolean = False
                    If Not _moduleConfiguration Is Nothing Then
                        blnHasModuleEditPermissions = (PortalSecurity.IsInRoles(_moduleConfiguration.AuthorizedEditRoles) = True) OrElse _
                            (PortalSecurity.IsInRoles(PortalSettings.ActiveTab.AdministratorRoles) = True) OrElse _
                            (PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = True)
                    End If

                    If blnPreview = False And blnHasModuleEditPermissions = True Then
                        _isEditable = 1
                    Else
                        _isEditable = 2
                    End If
                End If

                Return _isEditable = 1
            End Get
        End Property

        Public Property LocalResourceFile() As String
            Get
                Dim fileRoot As String

                If _localResourceFile = "" Then
                    fileRoot = Me.TemplateSourceDirectory & "/" & Services.Localization.Localization.LocalResourceDirectory & "/" & Me.ID
                Else
                    fileRoot = _localResourceFile
                End If
                Return fileRoot
            End Get
            Set(ByVal Value As String)
                _localResourceFile = Value
            End Set
        End Property

        Public ReadOnly Property ModulePath() As String
            Get
                Return Me.TemplateSourceDirectory & "/"
            End Get
        End Property

        Public ReadOnly Property ControlName() As String
            Get
                Return Me.GetType.Name.Replace("_", ".")
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public Property ModuleConfiguration() As ModuleInfo
            Get
                Return _moduleConfiguration
            End Get
            Set(ByVal Value As ModuleInfo)
                _moduleConfiguration = Value
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property PortalId() As Integer
            Get
                Return PortalSettings.PortalId
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property TabId() As Integer
            Get
                If Not _moduleConfiguration Is Nothing Then
                    Return Convert.ToInt32(_moduleConfiguration.TabID)
                Else
                    Return Null.NullInteger
                End If
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property TabModuleId() As Integer
            Get
                If Not _moduleConfiguration Is Nothing Then
                    Return Convert.ToInt32(_moduleConfiguration.TabModuleID)
                Else
                    Return Null.NullInteger
                End If
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property ModuleId() As Integer
            Get
                If Not _moduleConfiguration Is Nothing Then
                    Return Convert.ToInt32(_moduleConfiguration.ModuleID)
                Else
                    Return Null.NullInteger
                End If
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property UserInfo() As UserInfo
            Get
                Return UserController.GetCurrentUserInfo
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property UserId() As Integer
            Get
                If HttpContext.Current.Request.IsAuthenticated Then
                    UserId = UserInfo.UserID
                Else
                    UserId = -1
                End If
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property PortalAlias() As PortalAliasInfo
            Get
                Return PortalSettings.PortalAlias
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property Settings() As Hashtable
            Get
                If _settings Is Nothing Then
                    _settings = Portals.PortalSettings.GetTabModuleSettings( _
                                    New Hashtable(Portals.PortalSettings.GetModuleSettings(ModuleId)), _
                                    New Hashtable(Portals.PortalSettings.GetTabModuleSettings(TabModuleId)))
                End If
                Return _settings
            End Get
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)> _
        Public ReadOnly Property ContainerControl() As Control
            Get
                Return FindControlRecursive(Me, "ctr" & ModuleId.ToString)
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Function CanAddAction(ByVal access As SecurityAccessLevel, ByVal checkAdminControl As Boolean) As Boolean
            Dim canAdd As Boolean
            canAdd = PortalSecurity.HasNecessaryPermission(access, PortalSettings, ModuleConfiguration, UserInfo)
            If canAdd AndAlso checkAdminControl Then
                canAdd = EditMode AndAlso Not ModuleConfiguration.IsAdmin AndAlso Not IsAdminControl()
            End If
            Return canAdd
        End Function

        Private Function GetActionsCount(ByVal count As Integer, ByVal actions As ModuleActionCollection) As Integer

            For Each action As ModuleAction In actions
                If action.HasChildren Then
                    count += action.Actions.Count

                    'Recursively call to see if this collection has any child actions that would affect the count
                    count = GetActionsCount(count, action.Actions)
                End If
            Next

            Return count

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' LoadActions loads the Actions collections
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''     [cnurse]    01/19/2006  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub LoadActions()

            _actions = New ModuleActionCollection
            Dim maxActionId As Integer = Null.NullInteger

            'check if module Implements Entities.Modules.IActionable interface
            If TypeOf Me Is IActionable Then
                ' load module actions
                Dim ModuleActions As ModuleActionCollection = DirectCast(Me, IActionable).ModuleActions

                For Each action As ModuleAction In ModuleActions
                    If CanAddAction(action.Secure, False) Then
                        If action.Icon = "" Then
                            action.Icon = "edit.gif"
                        End If
                        If action.ID > maxActionId Then
                            maxActionId = action.ID
                        End If
                        _actions.Add(action)
                    End If
                Next
            End If

            'Make sure the Next Action Id counter is correct
            Dim actionCount As Integer = GetActionsCount(_actions.Count(), _actions)
            If _nextActionId < maxActionId Then
                _nextActionId = maxActionId
            End If
            If _nextActionId < actionCount Then
                _nextActionId = actionCount
            End If

            If Not String.IsNullOrEmpty(ModuleConfiguration.BusinessControllerClass) Then
                ' check if module implements IPortable interface, and user has Admin permissions
                If ModuleConfiguration.IsPortable And CanAddAction(SecurityAccessLevel.Admin, True) Then
                    _actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.ImportModule, Localization.GlobalResourceFile), "", "", "action_import.gif", NavigateURL(PortalSettings.ActiveTab.TabID, "ImportModule", "moduleid=" & ModuleId.ToString), "", False, SecurityAccessLevel.Admin, EditMode, False)
                    _actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.ExportModule, Localization.GlobalResourceFile), "", "", "action_export.gif", NavigateURL(PortalSettings.ActiveTab.TabID, "ExportModule", "moduleid=" & ModuleId.ToString), "", False, SecurityAccessLevel.Admin, EditMode, False)
                End If

                'If TypeOf objPortalModuleBase Is ISearchable Then
                If ModuleConfiguration.IsSearchable And ModuleConfiguration.DisplaySyndicate And CanAddAction(SecurityAccessLevel.Anonymous, False) Then
                    _actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.SyndicateModule, Localization.GlobalResourceFile), ModuleActionType.SyndicateModule, "", "action_rss.gif", NavigateURL(PortalSettings.ActiveTab.TabID, "", "moduleid=" & ModuleId.ToString).Replace(glbDefaultPage, "RSS.aspx"), "", False, SecurityAccessLevel.Anonymous, True, True)
                End If
            End If

            ' help module actions available to content editors and administrators
            If DotNetNuke.Security.PortalSecurity.HasNecessaryPermission(SecurityAccessLevel.Edit, PortalSettings, ModuleConfiguration) And Request.QueryString("ctl") <> "Help" Then
                SetHelpVisibility()
            End If

            'Add Print Action
            If ModuleConfiguration.DisplayPrint And CanAddAction(SecurityAccessLevel.Anonymous, False) Then
                ' print module action available to everyone
                _actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.PrintModule, Localization.GlobalResourceFile), ModuleActionType.PrintModule, "", "action_print.gif", NavigateURL(TabId, "", "mid=" & ModuleId.ToString, "SkinSrc=" & QueryStringEncode("[G]" & SkinInfo.RootSkin & "/" & glbHostSkinFolder & "/" & "No Skin"), "ContainerSrc=" & QueryStringEncode("[G]" & SkinInfo.RootContainer & "/" & glbHostSkinFolder & "/" & "No Container"), "dnnprintmode=true"), "", False, SecurityAccessLevel.Anonymous, True, True)
            End If

            ' core module actions only available to administrators 
            If CanAddAction(SecurityAccessLevel.Admin, True) Then
                ' module settings
                _actions.Add(GetNextActionID, "~", "")
                _actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.ModuleSettings, Localization.GlobalResourceFile), ModuleActionType.ModuleSettings, "", "action_settings.gif", NavigateURL(TabId, "Module", "ModuleId=" & ModuleId.ToString), Secure:=SecurityAccessLevel.Admin, Visible:=True)
                _actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.DeleteModule, Localization.GlobalResourceFile), ModuleActionType.DeleteModule, ModuleConfiguration.ModuleID.ToString, "action_delete.gif", "", "confirm('" + DotNetNuke.UI.Utilities.ClientAPI.GetSafeJSString(Localization.GetString("DeleteModule.Confirm")) + "')", False, SecurityAccessLevel.Admin, True, False)
                If ModuleConfiguration.CacheTime <> 0 Then
                    _actions.Add(GetNextActionID, Localization.GetString(ModuleActionType.ClearCache, Localization.GlobalResourceFile), ModuleActionType.ClearCache, ModuleConfiguration.ModuleID.ToString, "action_refresh.gif", Secure:=SecurityAccessLevel.Admin, Visible:=True)
                End If

                ' module movement
                _actions.Add(GetNextActionID, "~", "")
                Dim MoveActionRoot As New ModuleAction(GetNextActionID, Localization.GetString(ModuleActionType.MoveRoot, Localization.GlobalResourceFile), "", "", "", "", "", False, SecurityAccessLevel.Admin, EditMode)

                ' move module up/down
                If Not ModuleConfiguration Is Nothing Then
                    SetMoveMenuVisibility(MoveActionRoot.Actions.Add(GetNextActionID(), Localization.GetString(ModuleActionType.MoveTop, Localization.GlobalResourceFile), ModuleActionType.MoveTop, ModuleConfiguration.PaneName, Icon:="action_top.gif", Secure:=SecurityAccessLevel.Admin, Visible:=EditMode))
                    SetMoveMenuVisibility(MoveActionRoot.Actions.Add(GetNextActionID(), Localization.GetString(ModuleActionType.MoveUp, Localization.GlobalResourceFile), ModuleActionType.MoveUp, ModuleConfiguration.PaneName, Icon:="action_up.gif", Secure:=SecurityAccessLevel.Admin, Visible:=EditMode))
                    SetMoveMenuVisibility(MoveActionRoot.Actions.Add(GetNextActionID(), Localization.GetString(ModuleActionType.MoveDown, Localization.GlobalResourceFile), ModuleActionType.MoveDown, ModuleConfiguration.PaneName, Icon:="action_down.gif", Secure:=SecurityAccessLevel.Admin, Visible:=EditMode))
                    SetMoveMenuVisibility(MoveActionRoot.Actions.Add(GetNextActionID(), Localization.GetString(ModuleActionType.MoveBottom, Localization.GlobalResourceFile), ModuleActionType.MoveBottom, ModuleConfiguration.PaneName, Icon:="action_bottom.gif", Secure:=SecurityAccessLevel.Admin, Visible:=EditMode))
                End If

                ' move module to pane
                Dim intItem As Integer
                For intItem = 0 To PortalSettings.ActiveTab.Panes.Count - 1
                    SetMoveMenuVisibility(MoveActionRoot.Actions.Add(GetNextActionID(), Localization.GetString(ModuleActionType.MovePane, Localization.GlobalResourceFile) & " " & Convert.ToString(PortalSettings.ActiveTab.Panes(intItem)), ModuleActionType.MovePane, Convert.ToString(PortalSettings.ActiveTab.Panes(intItem)), Icon:="action_move.gif", Secure:=SecurityAccessLevel.Admin, Visible:=EditMode))
                Next intItem
                Dim ma As ModuleAction
                For Each ma In MoveActionRoot.Actions
                    If ma.Visible Then
                        _actions.Add(MoveActionRoot)
                        Exit For
                    End If
                Next
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' SetHelpVisibility Adds the Help actions to the Action Menu
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	05/12/2005	Documented
        '''     [cnurse]    01/19/2006  Moved from ActionBase
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SetHelpVisibility()

            'Add Help Menu Action
            Dim helpAction As New ModuleAction(GetNextActionID)
            helpAction.Title = Localization.GetString(ModuleActionType.ModuleHelp, Localization.GlobalResourceFile)
            helpAction.CommandName = ModuleActionType.ModuleHelp
            helpAction.CommandArgument = ""
            helpAction.Icon = "action_help.gif"
            helpAction.Url = NavigateURL(TabId, "Help", "ctlid=" & ModuleConfiguration.ModuleControlId.ToString, "moduleid=" & ModuleId)
            helpAction.Secure = SecurityAccessLevel.Edit
            helpAction.Visible = True
            helpAction.NewWindow = False
            helpAction.UseActionEvent = True
            _actions.Add(helpAction)


            'Add OnLine Help Action
            Dim helpUrl As String = GetOnLineHelp(ModuleConfiguration.HelpUrl, ModuleConfiguration)
            If Not Null.IsNull(helpUrl) Then
                'Add OnLine Help menu action
                helpAction = New ModuleAction(GetNextActionID)
                helpAction.Title = Localization.GetString(ModuleActionType.OnlineHelp, Localization.GlobalResourceFile)
                helpAction.CommandName = ModuleActionType.OnlineHelp
                helpAction.CommandArgument = ""
                helpAction.Icon = "action_help.gif"
                helpAction.Url = FormatHelpUrl(helpUrl, PortalSettings, ModuleConfiguration.FriendlyName)
                helpAction.Secure = SecurityAccessLevel.Edit
                helpAction.UseActionEvent = True
                helpAction.Visible = True
                helpAction.NewWindow = True
                _actions.Add(helpAction)
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' SetMoveMenuVisibility Adds the Move actions to the Action Menu
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	05/12/2005	Documented
        '''     [cnurse]    01/19/2006  Moved from ActionBase
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SetMoveMenuVisibility(ByVal Action As ModuleAction)

            Select Case Action.CommandName
                Case ModuleActionType.MoveTop
                    Action.Visible = (ModuleConfiguration.ModuleOrder <> 0) And (ModuleConfiguration.PaneModuleIndex > 0) And EditMode
                Case ModuleActionType.MoveUp
                    Action.Visible = (ModuleConfiguration.ModuleOrder <> 0) And (ModuleConfiguration.PaneModuleIndex > 0) And EditMode
                Case ModuleActionType.MoveDown
                    Action.Visible = (ModuleConfiguration.ModuleOrder <> 0) And (ModuleConfiguration.PaneModuleIndex < (ModuleConfiguration.PaneModuleCount - 1)) And EditMode
                Case ModuleActionType.MoveBottom
                    Action.Visible = (ModuleConfiguration.ModuleOrder <> 0) And (ModuleConfiguration.PaneModuleIndex < (ModuleConfiguration.PaneModuleCount - 1)) And EditMode
                Case ModuleActionType.MovePane
                    Action.Visible = (LCase(ModuleConfiguration.PaneName) <> LCase(Action.CommandArgument)) And EditMode
            End Select

        End Sub

#End Region

#Region "Protected Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Helper method that can be used to add an ActionEventHandler to the Skin for this 
        ''' Module Control
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 17/9/2004  Added Documentation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Sub AddActionHandler(ByVal e As ActionEventHandler)

            'This finds a reference to the containing skin
            Dim ParentSkin As DotNetNuke.UI.Skins.Skin = DotNetNuke.UI.Skins.Skin.GetParentSkin(Me)
            'We should always have a ParentSkin, but need to make sure
            If Not ParentSkin Is Nothing Then
                'Register our EventHandler as a listener on the ParentSkin so that it may tell us 
                'when a menu has been clicked.
                ParentSkin.RegisterModuleActionEvent(Me.ModuleId, e)
            End If


        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CreateChildControls method is called when the ASP.NET Page Framework
        ''' determines that it is time to instantiate a server control.
        ''' This method and attempts to resolve any previously cached output of the portal 
        ''' module from the ASP.NET cache.  
        ''' If it doesn't find cached output from a previous request, then it will instantiate 
        ''' and add the portal modules UserControl instance into the page tree.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 17/9/2004  Added Documentation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub CreateChildControls()

            If Not _moduleConfiguration Is Nothing Then

                ' if user does not have Edit rights for the module or is currently in View mode
                If PortalSecurity.HasNecessaryPermission(SecurityAccessLevel.Edit, PortalSettings, _moduleConfiguration, UserInfo.Username) = False OrElse PortalSettings.UserMode = Portals.PortalSettings.Mode.View Then
                    ' if the module supports caching and caching is enabled for the instance
                    If _moduleConfiguration.DefaultCacheTime <> -1 AndAlso _moduleConfiguration.CacheTime <> 0 Then
                        ' attempt to retrieve previously cached content 
                        If CacheMethod <> "D" Then
                            ' load from memory
                            _cachedOutput = Convert.ToString(DataCache.GetCache(ModuleController.CacheKey(TabModuleId)))
                        Else
                            ' load from disk 
                            If File.Exists(ModuleController.CacheFileName(TabModuleId)) Then
                                Dim cacheFile As New FileInfo(ModuleController.CacheFileName(TabModuleId))
                                If cacheFile.LastWriteTime.AddSeconds(_moduleConfiguration.CacheTime) >= Now Then
                                    Try
                                        ' acquire a reader lock
                                        objReaderWriterLock.AcquireReaderLock(ReaderTimeOut)
                                        ' load from cache file
                                        Try
                                            Dim objStreamReader As StreamReader
                                            objStreamReader = cacheFile.OpenText()
                                            _cachedOutput = objStreamReader.ReadToEnd
                                            objStreamReader.Close()
                                        Catch
                                            ' an error occured reading the file from disk
                                            _cachedOutput = ""
                                        Finally
                                            ' ensure the reader lock is released
                                            objReaderWriterLock.ReleaseReaderLock()
                                        End Try
                                    Catch
                                        ' the reader lock request timed out
                                        _cachedOutput = ""
                                    End Try
                                End If
                            End If
                        End If
                    End If

                    ' If no cached content is found, then instantiate and add the portal module user control into the portal's page server control tree
                    If _cachedOutput = "" AndAlso _moduleConfiguration.DefaultCacheTime <> -1 AndAlso _moduleConfiguration.CacheTime > 0 Then

                        MyBase.CreateChildControls()

                        Dim objPortalModuleBase As Entities.Modules.PortalModuleBase = CType(Page.LoadControl(_moduleConfiguration.ControlSrc), PortalModuleBase)
                        objPortalModuleBase.ModuleConfiguration = Me.ModuleConfiguration

                        ' set the control ID to the resource file name ( ie. controlname.ascx = controlname )
                        ' this is necessary for the Localization in PageBase
                        objPortalModuleBase.ID = Path.GetFileNameWithoutExtension(_moduleConfiguration.ControlSrc)

                        ' In skin.vb, the call to Me.Controls.Add(objPortalModuleBase) calls CreateChildControls() therefore
                        ' we need to indicate the control has already been created. We will manipulate the CacheTime property for this purpose. 
                        objPortalModuleBase.ModuleConfiguration.CacheTime = -(objPortalModuleBase.ModuleConfiguration.CacheTime)

                        Me.Controls.Add(objPortalModuleBase)

                    Else
                        ' restore the CacheTime property in preparation for the Render() event
                        If _moduleConfiguration.CacheTime < 0 Then
                            _moduleConfiguration.CacheTime = -(_moduleConfiguration.CacheTime)
                        End If
                    End If

                End If
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The Render method is called when the ASP.NET Page Framework
        ''' determines that it is time to render content into the page output stream.
        ''' This method and captures the output generated by the portal module user control
        ''' It then adds this content into the ASP.NET Cache for future requests.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 17/9/2004  Added Documentation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub Render(ByVal output As HtmlTextWriter)

            If Not _moduleConfiguration Is Nothing Then

                ' if the module supports caching and caching is enabled for the instance and the user does not have Edit rights or is currently in View mode
                If _moduleConfiguration.DefaultCacheTime <> -1 AndAlso _moduleConfiguration.CacheTime <> 0 AndAlso (PortalSecurity.HasNecessaryPermission(SecurityAccessLevel.Edit, PortalSettings, _moduleConfiguration, UserInfo.Username) = False Or PortalSettings.UserMode = PortalSettings.Mode.View) Then
                    ' use output caching
                    If _cachedOutput = "" Then
                        ' if no cached output exists render child controls into a TextWriter and save the results
                        Dim tempWriter As StringWriter = New StringWriter
                        MyBase.Render(New HtmlTextWriter(tempWriter))
                        _cachedOutput = tempWriter.ToString()
                    End If

                    If _cachedOutput <> "" Then
                        ' if cached content exists
                        If CacheMethod <> "D" Then
                            ' cache to memory
                            DataCache.SetCache(ModuleController.CacheKey(TabModuleId), _cachedOutput, DateTime.Now.AddSeconds(_moduleConfiguration.CacheTime))
                        Else
                            ' cache to disk
                            Dim blnUpdateCache As Boolean = False
                            If File.Exists(ModuleController.CacheFileName(TabModuleId)) = False Then
                                blnUpdateCache = True
                            Else
                                Dim cacheFile As New FileInfo(ModuleController.CacheFileName(TabModuleId))
                                If cacheFile.LastWriteTime.AddSeconds(_moduleConfiguration.CacheTime) < Now Then
                                    blnUpdateCache = True
                                End If
                            End If
                            If blnUpdateCache Then
                                Try
                                    ' acquire a writer lock
                                    objReaderWriterLock.AcquireWriterLock(WriterTimeOut)
                                    ' write to cache file
                                    Try
                                        If Not Directory.Exists(ModuleController.CacheDirectory) Then
                                            Directory.CreateDirectory(ModuleController.CacheDirectory)
                                        End If
                                        Dim objStream As StreamWriter
                                        objStream = File.CreateText(ModuleController.CacheFileName(TabModuleId))
                                        objStream.Write(_cachedOutput)
                                        objStream.Close()
                                    Catch
                                        ' an error occured writing the file to disk
                                    Finally
                                        ' ensure the writer lock is released
                                        objReaderWriterLock.ReleaseWriterLock()
                                    End Try
                                Catch
                                    ' the writer lock request timed out
                                End Try
                            End If
                        End If

                        ' output the cached content
                        output.Write(_cachedOutput)

                    Else ' no cached content

                        ' render the child control tree 
                        MyBase.Render(output)

                    End If

                Else ' no caching

                    ' render the child control tree 
                    MyBase.Render(output)

                End If

            Else ' no configuration

                ' render render the child control tree
                MyBase.Render(output)

            End If

        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the Next Action ID
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 03/02/2006  Added Documentation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetNextActionID() As Integer
            _nextActionId += 1
            Return _nextActionId
        End Function

        Public Function HasModulePermission(ByVal PermissionKey As String) As Boolean
            Return Security.Permissions.ModulePermissionController.HasModulePermission(Me.ModuleConfiguration.ModulePermissions, PermissionKey)
        End Function

#End Region

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CacheDirectory property is used to return the location of the "Cache"
        ''' Directory for the Module
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 04/28/2005  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        ''' 
        <Obsolete("This property is deprecated.  Plaese use ModuleController.CacheDirectory()")> _
        Public ReadOnly Property CacheDirectory() As String
            Get
                Return ModuleController.CacheDirectory()
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CacheFileName property is used to store the FileName for this Module's
        ''' Cache
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 04/28/2005  Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Obsolete("This property is deprecated.  Plaese use ModuleController.CacheFileName(TabModuleID)")> _
        Public ReadOnly Property CacheFileName() As String
            Get
                Return ModuleController.CacheFileName(TabModuleId)
            End Get
        End Property

        <Obsolete("This property is deprecated.  Plaese use ModuleController.CacheFileName(TabModuleID)")> _
        Public ReadOnly Property CacheFileName(ByVal TabModuleID As Integer) As String
            Get
                Return ModuleController.CacheFileName(TabModuleID)
            End Get
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' The CacheKey property is used to calculate a "unique" cache key
        ''' entry to be used to store/retrieve the portal module's content
        ''' from the ASP.NET Cache. Note that cache key allows two versions of the module
        ''' content to be stored - one for anonymous and one for authenticated users.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''   [cnurse] 17/9/2004  Added Documentation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Obsolete("This property is deprecated.  Plaese use ModuleController.CacheKey(TabModuleID)")> _
        Public ReadOnly Property CacheKey() As String
            Get
                Return ModuleController.CacheKey(TabModuleId)
            End Get
        End Property

        <Obsolete("This property is deprecated.  Plaese use ModuleController.CacheKey(TabModuleID)")> _
        Public ReadOnly Property CacheKey(ByVal TabModuleID As Integer) As String
            Get
                Return ModuleController.CacheKey(TabModuleID)
            End Get
        End Property

        <Obsolete("This method is deprecated.  Plaese use ModuleController.SynchronizeModule(ModuleId)")> _
        Public Sub SynchronizeModule()
            ModuleController.SynchronizeModule(ModuleId)
        End Sub


    End Class

End Namespace
