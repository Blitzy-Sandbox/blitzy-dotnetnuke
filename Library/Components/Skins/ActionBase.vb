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

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Actions
Imports DotNetNuke.Security
Imports DotNetNuke.UI.Skins
Imports DotNetNuke.UI.WebControls

Namespace DotNetNuke.UI.Containers

    ''' -----------------------------------------------------------------------------
    ''' Project	 : DotNetNuke
    ''' Namespace: DotNetNuke.UI.Containers
    ''' Class	 : ActionBase
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' ActionBase is the base for the Action objects 
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	10/07/2004	Documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public MustInherit Class ActionBase
        Inherits UI.Skins.SkinObjectBase

#Region "Private Members"

        Private _editMode As Boolean = False
        Private _moduleConfiguration As Entities.Modules.ModuleInfo
        Private _portalModule As Entities.Modules.PortalModuleBase

#End Region

#Region "Protected Members"

        Protected m_adminControl As Boolean = False
        Protected m_adminModule As Boolean = False
        Protected m_menuActions As ModuleActionCollection
        Protected m_menuActionRoot As ModuleAction
        Protected m_supportsIcons As Boolean = True
        Protected m_tabPreview As Boolean = False

#End Region

#Region "Events"

        Public Event Action As ActionEventHandler

#End Region

#Region "Properties"

        Public ReadOnly Property ActionRoot() As ModuleAction
            Get
                If m_menuActionRoot Is Nothing Then
                    m_menuActionRoot = New ModuleAction(GetNextActionID(), " ", "", "", "action.gif")
                End If
                Return m_menuActionRoot
            End Get
        End Property

        Public ReadOnly Property EditMode() As Boolean
            Get
                If Not PortalModule.ModuleConfiguration Is Nothing Then
                    _editMode = PortalSecurity.IsInRoles(PortalModule.PortalSettings.AdministratorRoleName) Or PortalSecurity.IsInRoles(PortalModule.PortalSettings.ActiveTab.AdministratorRoles.ToString)
                End If
                Return _editMode
            End Get
        End Property

        Public ReadOnly Property MenuActions() As ModuleActionCollection
            Get
                If m_menuActions Is Nothing Then
                    m_menuActions = New ModuleActionCollection
                End If
                Return m_menuActions
            End Get
        End Property

        Public ReadOnly Property ModuleConfiguration() As ModuleInfo
            Get
                _moduleConfiguration = PortalModule.ModuleConfiguration
                Return _moduleConfiguration
            End Get
        End Property

        Public Property PortalModule() As PortalModuleBase
            Get
                Return _portalModule
            End Get
            Set(ByVal Value As PortalModuleBase)
                _portalModule = Value
                If Not PortalModule.PortalSettings.ActiveTab.IsAdminTab Then
                    m_tabPreview = (PortalSettings.UserMode = PortalSettings.Mode.View)
                End If
            End Set
        End Property

        Public ReadOnly Property SupportsIcons() As Boolean
            Get
                Return m_supportsIcons
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Sub ClearCache(ByVal Command As ModuleAction)

            ' synchronize cache
            ModuleController.SynchronizeModule(PortalModule.ModuleId)

            ' Redirect to the same page to pick up changes
            Response.Redirect(Request.RawUrl, True)

        End Sub

        Private Sub Delete(ByVal Command As ModuleAction)

            Dim objModules As New ModuleController

            Dim objModule As ModuleInfo = objModules.GetModule(Integer.Parse(Command.CommandArgument), PortalModule.TabId, True)
            If Not objModule Is Nothing Then
                objModules.DeleteTabModule(PortalModule.TabId, Integer.Parse(Command.CommandArgument))

                Dim m_UserInfo As UserInfo = UserController.GetCurrentUserInfo
                Dim objEventLog As New Services.Log.EventLog.EventLogController
                objEventLog.AddLog(objModule, PortalSettings, m_UserInfo.UserID, "", Services.Log.EventLog.EventLogController.EventLogType.MODULE_SENT_TO_RECYCLE_BIN)
            End If

            ' Redirect to the same page to pick up changes
            Response.Redirect(Request.RawUrl, True)

        End Sub

        Private Sub DoAction(ByVal Command As ModuleAction)
            If Command.NewWindow Then
                Response.Write("<script>window.open('" & Command.Url & "','_blank')</script>")
            Else
                Response.Redirect(Command.Url, True)
            End If
        End Sub

        Private Sub MoveToPane(ByVal Command As ModuleAction)

            Dim objModules As New ModuleController

            objModules.UpdateModuleOrder(PortalModule.TabId, PortalModule.ModuleConfiguration.ModuleID, -1, Command.CommandArgument)
            objModules.UpdateTabModuleOrder(PortalModule.TabId, PortalModule.PortalId)

            ' Redirect to the same page to pick up changes
            Response.Redirect(Request.RawUrl, True)

        End Sub

        Private Sub MoveUpDown(ByVal Command As ModuleAction)

            Dim objModules As New ModuleController
            Select Case Command.CommandName
                Case ModuleActionType.MoveTop
                    objModules.UpdateModuleOrder(PortalModule.TabId, PortalModule.ModuleConfiguration.ModuleID, 0, Command.CommandArgument)
                Case ModuleActionType.MoveUp
                    objModules.UpdateModuleOrder(PortalModule.TabId, PortalModule.ModuleConfiguration.ModuleID, PortalModule.ModuleConfiguration.ModuleOrder - 3, Command.CommandArgument)
                Case ModuleActionType.MoveDown
                    objModules.UpdateModuleOrder(PortalModule.TabId, PortalModule.ModuleConfiguration.ModuleID, PortalModule.ModuleConfiguration.ModuleOrder + 3, Command.CommandArgument)
                Case ModuleActionType.MoveBottom
                    objModules.UpdateModuleOrder(PortalModule.TabId, PortalModule.ModuleConfiguration.ModuleID, (PortalModule.ModuleConfiguration.PaneModuleCount * 2) + 1, Command.CommandArgument)
            End Select

            objModules.UpdateTabModuleOrder(PortalModule.TabId, PortalModule.PortalId)

            ' Redirect to the same page to pick up changes
            Response.Redirect(Request.RawUrl, True)

        End Sub

#End Region

#Region "Protected Methods"

        Protected Function DisplayControl(ByVal objNodes As DNNNodeCollection) As Boolean
            If Not objNodes Is Nothing AndAlso objNodes.Count > 0 AndAlso m_tabPreview = False Then
                Dim objRootNode As DNNNode = objNodes(0)
                If objRootNode.HasNodes AndAlso objRootNode.DNNNodes.Count = 0 Then
                    'if has pending node then display control
                    Return True
                ElseIf objRootNode.DNNNodes.Count > 0 Then
                    'verify that at least one child is not a break
                    For Each childNode As DNNNode In objRootNode.DNNNodes
                        If Not childNode.IsBreak Then
                            'Found a child so make Visible
                            Return True
                        End If
                    Next
                End If
            End If
            Return False
        End Function

        Protected Function GetNextActionID() As Integer
            Dim retValue As Integer = Null.NullInteger
            If Not PortalModule Is Nothing Then
                retValue = PortalModule.GetNextActionID()
            End If
            Return retValue
        End Function

        <Obsolete("This function has been obsoleted: Use the no parameters version GetNextActionID()")> _
        Protected Function GetNextActionID(ByVal ModAction As ModuleAction, ByVal Level As Integer) As Integer
            Return PortalModule.GetNextActionID()
        End Function

        Protected Overridable Sub OnAction(ByVal e As ActionEventArgs)
            RaiseEvent Action(Me, e)
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary></summary>
        ''' <remarks></remarks>
        ''' <history>
        ''' 	[Nik Kalyani]	10/15/2004	Replaced Optional parameters with multiple method signatures
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String)
            AddAction(Title, CmdName, "", "", "", False, SecurityAccessLevel.Anonymous, False, False)
        End Sub

        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String, ByVal CmdArg As String)
            AddAction(Title, CmdName, CmdArg, "", "", False, SecurityAccessLevel.Anonymous, False, False)
        End Sub

        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String, ByVal CmdArg As String, ByVal Icon As String)
            AddAction(Title, CmdName, CmdArg, Icon, "", False, SecurityAccessLevel.Anonymous, False, False)
        End Sub

        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String, ByVal CmdArg As String, ByVal Icon As String, ByVal Url As String)
            AddAction(Title, CmdName, CmdArg, Icon, Url, False, SecurityAccessLevel.Anonymous, False, False)
        End Sub

        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String, ByVal CmdArg As String, ByVal Icon As String, ByVal Url As String, ByVal UseActionEvent As Boolean)
            AddAction(Title, CmdName, CmdArg, Icon, Url, UseActionEvent, SecurityAccessLevel.Anonymous, False, False)
        End Sub

        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String, ByVal CmdArg As String, ByVal Icon As String, ByVal Url As String, ByVal UseActionEvent As Boolean, ByVal Secure As SecurityAccessLevel)
            AddAction(Title, CmdName, CmdArg, Icon, Url, UseActionEvent, Secure, False, False)
        End Sub

        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String, ByVal CmdArg As String, ByVal Icon As String, ByVal Url As String, ByVal UseActionEvent As Boolean, ByVal Secure As SecurityAccessLevel, ByVal Visible As Boolean)
            AddAction(Title, CmdName, CmdArg, Icon, Url, UseActionEvent, Secure, Visible, False)
        End Sub

        Public Sub AddAction(ByVal Title As String, ByVal CmdName As String, ByVal CmdArg As String, ByVal Icon As String, ByVal Url As String, ByVal UseActionEvent As Boolean, ByVal Secure As SecurityAccessLevel, ByVal Visible As Boolean, ByVal NewWindow As Boolean)
            Dim Action As ModuleAction = MenuActions.Add(GetNextActionID, Title, CmdName, CmdArg, Icon, Url, UseActionEvent, Secure, Visible, NewWindow)
        End Sub

        Public Function GetAction(ByVal Index As Integer) As ModuleAction
            Return GetAction(Index, ActionRoot)
        End Function

        Public Function GetAction(ByVal Index As Integer, ByVal ParentAction As ModuleAction) As ModuleAction
            Dim retAction As ModuleAction = Nothing
            If Not ParentAction Is Nothing Then
                Dim modaction As ModuleAction
                For Each modaction In ParentAction.Actions
                    If modaction.ID = Index Then
                        retAction = modaction
                        Exit For
                    End If
                    If modaction.HasChildren Then
                        Dim ChildAction As ModuleAction = GetAction(Index, modaction)
                        If Not ChildAction Is Nothing Then
                            retAction = ChildAction
                            Exit For
                        End If
                    End If
                Next
            End If
            Return retAction
        End Function

        Public Sub ProcessAction(ByVal ActionID As String)
            If IsNumeric(ActionID) Then
                Dim action As ModuleAction = GetAction(Convert.ToInt32(ActionID))
                Select Case action.CommandName
                    Case ModuleActionType.ModuleHelp
                        DoAction(action)
                    Case ModuleActionType.OnlineHelp
                        DoAction(action)
                    Case ModuleActionType.ModuleSettings
                        DoAction(action)
                    Case ModuleActionType.DeleteModule
                        Delete(action)
                    Case ModuleActionType.PrintModule
                        DoAction(action)
                    Case ModuleActionType.ClearCache
                        ClearCache(action)
                    Case ModuleActionType.MovePane
                        MoveToPane(action)
                    Case ModuleActionType.MoveTop, ModuleActionType.MoveUp, ModuleActionType.MoveDown, ModuleActionType.MoveBottom
                        MoveUpDown(action)
                    Case Else       ' custom action
                        If action.Url.Length > 0 And action.UseActionEvent = False Then
                            DoAction(action)
                        Else
                            OnAction(New ActionEventArgs(action, ModuleConfiguration))
                        End If
                End Select
            End If
        End Sub

#End Region

#Region "Event Handlers"

        Private Sub Page_Init(ByVal sender As Object, ByVal e As System.EventArgs) Handles MyBase.Init

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Page_Load runs when the class is loaded
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	05/12/2005	Documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles MyBase.Load
            Try
                ActionRoot.Actions.AddRange(MenuActions)
            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

#End Region

#Region "Obsolete"

        <Obsolete("This property has been obsoleted: Use EditMode")> _
        Protected Property m_editMode() As Boolean
            Get
                Return _editMode
            End Get
            Set(ByVal Value As Boolean)
                _editMode = Value
            End Set
        End Property

        <Obsolete("This property has been obsoleted: Use ModuleConfiguration")> _
        Protected Property m_moduleConfiguration() As ModuleInfo
            Get
                Return ModuleConfiguration
            End Get
            Set(ByVal Value As ModuleInfo)
                _moduleConfiguration = Value
            End Set
        End Property

        <Obsolete("This property has been obsoleted: Use PortalModule")> _
        Protected Property m_PortalModule() As Entities.Modules.PortalModuleBase
            Get
                Return PortalModule
            End Get
            Set(ByVal Value As PortalModuleBase)
                PortalModule = Value
            End Set
        End Property

#End Region

    End Class

End Namespace
