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

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Definitions
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Security.Permissions

Namespace DotNetNuke.UI.ControlPanels

	''' -----------------------------------------------------------------------------
	''' <summary>
	''' The ControlPanel class defines a custom base class inherited by all
	''' ControlPanel controls.
	''' </summary>
    ''' <remarks>
	''' </remarks>
	''' <history>
	''' </history>
	''' -----------------------------------------------------------------------------
    Public Class ControlPanelBase
        Inherits System.Web.UI.UserControl

#Region "Enums"

        Protected Enum ViewPermissionType
            View = 0
            Edit = 1
        End Enum

#End Region

#Region "Private Members"

        Private _localResourceFile As String

#End Region

#Region "Protected Properties"

        Protected ReadOnly Property ShowContent() As Boolean
            Get
                Return PortalSettings.ContentVisible
            End Get
        End Property

        Protected ReadOnly Property IsPreview() As Boolean
            Get
                If PortalSettings.UserMode = PortalSettings.Mode.Edit Then
                    Return False
                Else
                    Return True
                End If
            End Get
        End Property

        Protected ReadOnly Property IsVisible() As Boolean
            Get
                Return PortalSettings.ControlPanelVisible
            End Get
        End Property

#End Region

#Region "Private Methods"

        Private Function AddModulePermission(ByVal moduleId As Integer, ByVal permission As PermissionInfo, ByVal roleId As Integer) As ModulePermissionInfo
            Dim objRoles As New Security.Roles.RoleController
            Dim objRole As Security.Roles.RoleInfo
            Dim roleName As String = Null.NullString
            Dim objModulePermission As New ModulePermissionInfo
            objModulePermission.ModuleID = moduleId
            objModulePermission.PermissionID = permission.PermissionID
            objModulePermission.RoleID = roleId
            objModulePermission.PermissionKey = permission.PermissionKey
            objModulePermission.AllowAccess = False

            ' allow access to the permission if the role is in the list of administrator roles for the page
            Select Case roleId
                Case Integer.Parse(glbRoleUnauthUser)
                    roleName = glbRoleUnauthUserName
                Case Integer.Parse(glbRoleAllUsers)
                    roleName = glbRoleAllUsersName
                Case Else
                    objRole = objRoles.GetRole(objModulePermission.RoleID, PortalSettings.PortalId)
                    If Not objRole Is Nothing Then
                        roleName = objRole.RoleName
                    End If
            End Select
            If Not String.IsNullOrEmpty(roleName) Then
                If PortalSettings.ActiveTab.AdministratorRoles.IndexOf(roleName) <> -1 Then
                    objModulePermission.AllowAccess = True
                End If
            End If
            Return objModulePermission
        End Function

#End Region

#Region "Protected Methods"

        Protected Sub AddExistingModule(ByVal moduleId As Integer, ByVal tabId As Integer, ByVal paneName As String, ByVal position As Integer, ByVal align As String)

            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo
            Dim objEventLog As New Services.Log.EventLog.EventLogController

            Dim UserId As Integer = -1
            If Request.IsAuthenticated Then
                Dim objUserInfo As UserInfo = UserController.GetCurrentUserInfo
                UserId = objUserInfo.UserID
            End If

            objModule = objModules.GetModule(moduleId, tabId, False)
            If Not objModule Is Nothing Then
                ' clone the module object ( to avoid creating an object reference to the data cache )
                Dim objClone As ModuleInfo = objModule.Clone()
                objClone.TabID = PortalSettings.ActiveTab.TabID
                objClone.ModuleOrder = position
                objClone.PaneName = paneName
                objClone.Alignment = align
                objModules.AddModule(objClone)
                objEventLog.AddLog(objClone, PortalSettings, UserId, "", Services.Log.EventLog.EventLogController.EventLogType.MODULE_CREATED)
            End If

        End Sub

        Protected Sub AddNewModule(ByVal title As String, ByVal desktopModuleId As Integer, ByVal paneName As String, ByVal position As Integer, ByVal permissionType As ViewPermissionType, ByVal align As String)

            Dim objTabPermissions As TabPermissionCollection = PortalSettings.ActiveTab.TabPermissions
            Dim objPermissionController As New PermissionController
            Dim objModules As New ModuleController
            Dim objModuleDefinitions As New ModuleDefinitionController
            Dim objModuleDefinition As ModuleDefinitionInfo
            Dim objEventLog As New Services.Log.EventLog.EventLogController
            Dim intIndex As Integer
            Dim j As Integer

            Try
                Dim objDesktopModules As New DesktopModuleController
                Dim arrDM As ArrayList = objDesktopModules.GetDesktopModulesByPortal(PortalSettings.PortalId)
                Dim intloop As Integer
                Dim isSelectable As Boolean = False
                For intloop = 0 To arrDM.Count - 1
                    If CType(arrDM(intloop), DesktopModuleInfo).DesktopModuleID = desktopModuleId Then
                        isSelectable = True
                        Exit For
                    End If
                Next
                If isSelectable = False Then Throw New System.Exception
            Catch ex As Exception
                Throw New System.Exception
            End Try

            Dim UserId As Integer = -1
            If Request.IsAuthenticated Then
                Dim objUserInfo As UserInfo = UserController.GetCurrentUserInfo
                UserId = objUserInfo.UserID
            End If

            Dim arrModuleDefinitions As ArrayList = objModuleDefinitions.GetModuleDefinitions(desktopModuleId)
            For intIndex = 0 To arrModuleDefinitions.Count - 1
                objModuleDefinition = CType(arrModuleDefinitions(intIndex), ModuleDefinitionInfo)

                Dim objModule As New ModuleInfo
                objModule.Initialize(PortalSettings.PortalId)

                objModule.PortalID = PortalSettings.PortalId
                objModule.TabID = PortalSettings.ActiveTab.TabID
                objModule.ModuleOrder = position
                If title = "" Then
                    objModule.ModuleTitle = objModuleDefinition.FriendlyName
                Else
                    objModule.ModuleTitle = title
                End If
                objModule.PaneName = paneName
                objModule.ModuleDefID = objModuleDefinition.ModuleDefID
                If objModuleDefinition.DefaultCacheTime > 0 Then
                    objModule.CacheTime = objModuleDefinition.DefaultCacheTime
                End If

                ' initialize module permissions
                Dim objModulePermissions As New ModulePermissionCollection
                objModule.ModulePermissions = objModulePermissions
                objModule.InheritViewPermissions = False

                ' get the default module view permissions
                Dim arrSystemModuleViewPermissions As ArrayList = objPermissionController.GetPermissionByCodeAndKey("SYSTEM_MODULE_DEFINITION", "VIEW")

                ' get the permissions from the page
                Dim objTabPermission As TabPermissionInfo
                For Each objTabPermission In objTabPermissions
                    ' get the system module permissions for the permissionkey
                    Dim arrSystemModulePermissions As ArrayList = objPermissionController.GetPermissionByCodeAndKey("SYSTEM_MODULE_DEFINITION", objTabPermission.PermissionKey)
                    ' loop through the system module permissions
                    For j = 0 To arrSystemModulePermissions.Count - 1
                        ' create the module permission
                        Dim objSystemModulePermission As PermissionInfo
                        objSystemModulePermission = CType(arrSystemModulePermissions(j), PermissionInfo)
                        Dim objModulePermission As ModulePermissionInfo = AddModulePermission(objModule.ModuleID, objSystemModulePermission, objTabPermission.RoleID)

                        ' add the permission to the collection
                        If Not objModulePermissions.Contains(objModulePermission) And objModulePermission.AllowAccess Then
                            objModulePermissions.Add(objModulePermission)
                        End If

                        ' ensure that every EDIT permission which allows access also provides VIEW permission
                        If objModulePermission.PermissionKey = "EDIT" And objModulePermission.AllowAccess Then
                            Dim objModuleViewperm As New ModulePermissionInfo
                            objModuleViewperm.ModuleID = objModulePermission.ModuleID
                            objModuleViewperm.PermissionID = CType(arrSystemModuleViewPermissions(0), PermissionInfo).PermissionID
                            objModuleViewperm.RoleID = objModulePermission.RoleID
                            objModuleViewperm.PermissionKey = "VIEW"
                            objModuleViewperm.AllowAccess = True
                            If Not objModulePermissions.Contains(objModuleViewperm) Then
                                objModulePermissions.Add(objModuleViewperm)
                            End If
                        End If
                    Next

                    'Get the custom Module Permissions,  Assume that roles with Edit Tab Permissions
                    'are automatically assigned to the Custom Module Permissions
                    If objTabPermission.PermissionKey = "EDIT" Then
                        Dim arrCustomModulePermissions As ArrayList = objPermissionController.GetPermissionsByModuleDefID(objModule.ModuleDefID)

                        ' loop through the custom module permissions
                        For j = 0 To arrCustomModulePermissions.Count - 1
                            ' create the module permission
                            Dim objCustomModulePermission As PermissionInfo
                            objCustomModulePermission = CType(arrCustomModulePermissions(j), PermissionInfo)
                            Dim objModulePermission As ModulePermissionInfo = AddModulePermission(objModule.ModuleID, objCustomModulePermission, objTabPermission.RoleID)

                            ' add the permission to the collection
                            If Not objModulePermissions.Contains(objModulePermission) And objModulePermission.AllowAccess Then
                                objModulePermissions.Add(objModulePermission)
                            End If
                        Next
                    End If
                Next

                Select Case permissionType
                    Case ViewPermissionType.View
                        objModule.InheritViewPermissions = True
                    Case ViewPermissionType.Edit
                        objModule.ModulePermissions = objModulePermissions
                End Select

                objModule.AllTabs = False
                objModule.Visibility = VisibilityState.Maximized
                objModule.Alignment = align

                objModules.AddModule(objModule)
                objEventLog.AddLog(objModule, PortalSettings, UserId, "", Services.Log.EventLog.EventLogController.EventLogType.MODULE_CREATED)
            Next

        End Sub

        Protected Function BuildURL(ByVal PortalID As Integer, ByVal FriendlyName As String) As String
            Dim strURL As String = "~/" & glbDefaultPage

            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo = objModules.GetModuleByDefinition(PortalID, FriendlyName)
            If Not objModule Is Nothing Then
                If PortalID = Null.NullInteger Then
                    strURL = NavigateURL(objModule.TabID, True)
                Else
                    strURL = NavigateURL(objModule.TabID)
                End If
            End If

            Return strURL
        End Function

        Protected Sub SetContentMode(ByVal showContent As Boolean)
            Personalization.Personalization.SetProfile("Usability", "ContentVisible" & PortalSettings.PortalId.ToString, showContent.ToString)
        End Sub

        Protected Sub SetPreviewMode(ByVal isPreview As Boolean)
            If isPreview Then
                Personalization.Personalization.SetProfile("Usability", "UserMode" & PortalSettings.PortalId.ToString, "View")
            Else
                Personalization.Personalization.SetProfile("Usability", "UserMode" & PortalSettings.PortalId.ToString, "Edit")
            End If
        End Sub

        Protected Sub SetVisibleMode(ByVal isVisible As Boolean)
            Personalization.Personalization.SetProfile("Usability", "ControlPanelVisible" & PortalSettings.PortalId.ToString, isVisible.ToString)
        End Sub

#End Region

#Region "Public Properties"

        Public ReadOnly Property PortalSettings() As PortalSettings
            Get
                PortalSettings = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)
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

#End Region

    End Class

End Namespace
