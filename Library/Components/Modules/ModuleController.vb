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
Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.Globalization
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Security.Permissions
Imports System.Xml
Imports DotNetNuke.Entities.Modules.Definitions
Imports System.Xml.Serialization
Imports System.IO
Imports DotNetNuke.Services.EventQueue
Imports DotNetNuke.Security.Roles

Namespace DotNetNuke.Entities.Modules

    Public Class ModuleController

#Region "Private Methods"

        Private Sub ClearCache(ByVal TabId As Integer)
            DataCache.ClearModuleCache(TabId)
        End Sub

        Private Function FillModuleInfo(ByVal dr As IDataReader) As ModuleInfo
            Return FillModuleInfo(dr, True, True, False)
        End Function

        Private Function FillModuleInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As ModuleInfo
            Return FillModuleInfo(dr, CheckForOpenDataReader, True, False)
        End Function

        Private Function FillModuleInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean, ByVal IncludePermissions As Boolean, ByVal CheckForLegacyFields As Boolean) As ModuleInfo
            Dim objModuleInfo As New ModuleInfo
            Dim objModulePermissionController As New Security.Permissions.ModulePermissionController
            ' read datareader
            Dim canContinue As Boolean = True

            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If
            If canContinue Then
                objModuleInfo.PortalID = Convert.ToInt32(Null.SetNull(dr("PortalID"), objModuleInfo.PortalID))
                objModuleInfo.TabID = Convert.ToInt32(Null.SetNull(dr("TabID"), objModuleInfo.TabID))
                objModuleInfo.TabModuleID = Convert.ToInt32(Null.SetNull(dr("TabModuleID"), objModuleInfo.TabModuleID))
                objModuleInfo.ModuleID = Convert.ToInt32(Null.SetNull(dr("ModuleID"), objModuleInfo.ModuleID))
                objModuleInfo.ModuleDefID = Convert.ToInt32(Null.SetNull(dr("ModuleDefID"), objModuleInfo.ModuleDefID))
                objModuleInfo.ModuleOrder = Convert.ToInt32(Null.SetNull(dr("ModuleOrder"), objModuleInfo.ModuleOrder))
                objModuleInfo.PaneName = Convert.ToString(Null.SetNull(dr("PaneName"), objModuleInfo.PaneName))
                objModuleInfo.ModuleTitle = Convert.ToString(Null.SetNull(dr("ModuleTitle"), objModuleInfo.ModuleTitle))
                objModuleInfo.CacheTime = Convert.ToInt32(Null.SetNull(dr("CacheTime"), objModuleInfo.CacheTime))
                objModuleInfo.Alignment = Convert.ToString(Null.SetNull(dr("Alignment"), objModuleInfo.Alignment))
                objModuleInfo.Color = Convert.ToString(Null.SetNull(dr("Color"), objModuleInfo.Color))
                objModuleInfo.Border = Convert.ToString(Null.SetNull(dr("Border"), objModuleInfo.Border))
                objModuleInfo.IconFile = Convert.ToString(Null.SetNull(dr("IconFile"), objModuleInfo.IconFile))
                objModuleInfo.AllTabs = Convert.ToBoolean(Null.SetNull(dr("AllTabs"), objModuleInfo.AllTabs))
                Dim intVisibility As Integer
                Select Case Convert.ToInt32(Null.SetNull(dr("Visibility"), intVisibility))
                    Case 0, Null.NullInteger : objModuleInfo.Visibility = VisibilityState.Maximized
                    Case 1 : objModuleInfo.Visibility = VisibilityState.Minimized
                    Case 2 : objModuleInfo.Visibility = VisibilityState.None
                End Select
                objModuleInfo.IsDeleted = Convert.ToBoolean(Null.SetNull(dr("IsDeleted"), objModuleInfo.IsDeleted))
                objModuleInfo.Header = Convert.ToString(Null.SetNull(dr("Header"), objModuleInfo.Header))
                objModuleInfo.Footer = Convert.ToString(Null.SetNull(dr("Footer"), objModuleInfo.Footer))
                objModuleInfo.StartDate = Convert.ToDateTime(Null.SetNull(dr("StartDate"), objModuleInfo.StartDate))
                objModuleInfo.EndDate = Convert.ToDateTime(Null.SetNull(dr("EndDate"), objModuleInfo.EndDate))
                objModuleInfo.ContainerSrc = Convert.ToString(Null.SetNull(dr("ContainerSrc"), objModuleInfo.ContainerSrc))
                objModuleInfo.DisplayTitle = Convert.ToBoolean(Null.SetNull(dr("DisplayTitle"), objModuleInfo.DisplayTitle))
                objModuleInfo.DisplayPrint = Convert.ToBoolean(Null.SetNull(dr("DisplayPrint"), objModuleInfo.DisplayPrint))
                objModuleInfo.DisplaySyndicate = Convert.ToBoolean(Null.SetNull(dr("DisplaySyndicate"), objModuleInfo.DisplaySyndicate))
                objModuleInfo.InheritViewPermissions = Convert.ToBoolean(Null.SetNull(dr("InheritViewPermissions"), objModuleInfo.InheritViewPermissions))
                objModuleInfo.DesktopModuleID = Convert.ToInt32(Null.SetNull(dr("DesktopModuleID"), objModuleInfo.DesktopModuleID))
                objModuleInfo.FolderName = Convert.ToString(Null.SetNull(dr("FolderName"), objModuleInfo.FolderName))
                objModuleInfo.FriendlyName = Convert.ToString(Null.SetNull(dr("FriendlyName"), objModuleInfo.FriendlyName))
                objModuleInfo.Description = Convert.ToString(Null.SetNull(dr("Description"), objModuleInfo.Description))
                objModuleInfo.Version = Convert.ToString(Null.SetNull(dr("Version"), objModuleInfo.Version))
                objModuleInfo.IsPremium = Convert.ToBoolean(Null.SetNull(dr("IsPremium"), objModuleInfo.IsPremium))
                objModuleInfo.IsAdmin = Convert.ToBoolean(Null.SetNull(dr("IsAdmin"), objModuleInfo.IsAdmin))
                objModuleInfo.BusinessControllerClass = Convert.ToString(Null.SetNull(dr("BusinessControllerClass"), objModuleInfo.BusinessControllerClass))
                objModuleInfo.ModuleName = Convert.ToString(Null.SetNull(dr("ModuleName"), objModuleInfo.ModuleName))
                objModuleInfo.SupportedFeatures = Convert.ToInt32(Null.SetNull(dr("SupportedFeatures"), objModuleInfo.SupportedFeatures))
                objModuleInfo.CompatibleVersions = Convert.ToString(Null.SetNull(dr("CompatibleVersions"), objModuleInfo.CompatibleVersions))
                objModuleInfo.Dependencies = Convert.ToString(Null.SetNull(dr("Dependencies"), objModuleInfo.Dependencies))
                objModuleInfo.Permissions = Convert.ToString(Null.SetNull(dr("Permissions"), objModuleInfo.Permissions))
                objModuleInfo.DefaultCacheTime = Convert.ToInt32(Null.SetNull(dr("DefaultCacheTime"), objModuleInfo.DefaultCacheTime))
                objModuleInfo.ModuleControlId = Convert.ToInt32(Null.SetNull(dr("ModuleControlId"), objModuleInfo.ModuleControlId))
                objModuleInfo.ControlSrc = Convert.ToString(Null.SetNull(dr("ControlSrc"), objModuleInfo.ControlSrc))
                Dim intControlType As Integer
                Select Case Convert.ToInt32(Null.SetNull(dr("ControlType"), intControlType))
                    Case -3 : objModuleInfo.ControlType = SecurityAccessLevel.ControlPanel
                    Case -2 : objModuleInfo.ControlType = SecurityAccessLevel.SkinObject
                    Case -1, Null.NullInteger : objModuleInfo.ControlType = SecurityAccessLevel.Anonymous
                    Case 0 : objModuleInfo.ControlType = SecurityAccessLevel.View
                    Case 1 : objModuleInfo.ControlType = SecurityAccessLevel.Edit
                    Case 2 : objModuleInfo.ControlType = SecurityAccessLevel.Admin
                    Case 3 : objModuleInfo.ControlType = SecurityAccessLevel.Host
                End Select
                objModuleInfo.ControlTitle = Convert.ToString(Null.SetNull(dr("ControlTitle"), objModuleInfo.ControlTitle))
                objModuleInfo.HelpUrl = Convert.ToString(Null.SetNull(dr("HelpUrl"), objModuleInfo.HelpUrl))
                objModuleInfo.SupportsPartialRendering = Convert.ToBoolean(Null.SetNull(dr("SupportsPartialRendering"), objModuleInfo.SupportsPartialRendering))

                If IncludePermissions Then
                    If Not objModuleInfo Is Nothing Then
                        'Get the Module permissions first (then we can parse the collection to determine the View/Edit Roles)
                        objModuleInfo.ModulePermissions = objModulePermissionController.GetModulePermissionsCollectionByModuleID(objModuleInfo.ModuleID, objModuleInfo.TabID)
                        objModuleInfo.AuthorizedEditRoles = objModulePermissionController.GetModulePermissions(objModuleInfo.ModulePermissions, "EDIT")
                        If objModuleInfo.InheritViewPermissions Then
                            Dim objTabPermissionController As New TabPermissionController
                            Dim objTabPermissionCollection As TabPermissionCollection = objTabPermissionController.GetTabPermissionsCollectionByTabID(objModuleInfo.TabID, objModuleInfo.PortalID)
                            objModuleInfo.AuthorizedViewRoles = objTabPermissionController.GetTabPermissions(objTabPermissionCollection, "VIEW")
                        Else
                            objModuleInfo.AuthorizedViewRoles = objModulePermissionController.GetModulePermissions(objModuleInfo.ModulePermissions, "VIEW")
                        End If
                        If CheckForLegacyFields Then
                            If objModuleInfo.AuthorizedEditRoles = ";" Then
                                ' this code is here for legacy support - the AuthorizedEditRoles were stored as a concatenated list of roleids prior to DNN 3.0
                                Try
                                    objModuleInfo.AuthorizedEditRoles = Convert.ToString(Null.SetNull(dr("AuthorizedEditRoles"), objModuleInfo.AuthorizedEditRoles))
                                Catch
                                    ' the AuthorizedEditRoles field was removed from the Modules table in 3.0
                                End Try
                            End If
                            If objModuleInfo.AuthorizedViewRoles = ";" Then
                                ' this code is here for legacy support - the AuthorizedViewRoles were stored as a concatenated list of roleids prior to DNN 3.0
                                Try
                                    objModuleInfo.AuthorizedViewRoles = Convert.ToString(Null.SetNull(dr("AuthorizedViewRoles"), objModuleInfo.AuthorizedViewRoles))
                                Catch
                                    ' the AuthorizedViewRoles field was removed from the Modules table in 3.0
                                End Try
                            End If
                        End If
                    End If
                End If
            Else
                objModuleInfo = Nothing
            End If
            Return objModuleInfo
        End Function

        Private Function FillModuleInfoCollection(ByVal dr As IDataReader) As ArrayList
            Return FillModuleInfoCollection(dr, True, False)
        End Function

        Private Function FillModuleInfoCollection(ByVal dr As IDataReader, ByVal IncludePermissions As Boolean, ByVal CheckForLegacyFields As Boolean) As ArrayList
            Dim arr As New ArrayList
            Try
                Dim obj As ModuleInfo
                While dr.Read
                    ' fill business object
                    obj = FillModuleInfo(dr, False, IncludePermissions, CheckForLegacyFields)
                    ' add to collection
                    arr.Add(obj)
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return arr
        End Function

        Private Function FillModuleInfoDictionary(ByVal dr As IDataReader) As Dictionary(Of Integer, ModuleInfo)
            Dim dic As New Dictionary(Of Integer, ModuleInfo)
            Try
                Dim obj As ModuleInfo
                While dr.Read
                    ' fill business object
                    obj = FillModuleInfo(dr, False, True, False)
                    ' add to dictionary
                    dic.Add(obj.ModuleID, obj)
                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return dic
        End Function

#End Region

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Adds module content to the node module
        ''' </summary>
        ''' <param name="nodeModule">Node where to add the content</param>
        ''' <param name="objModule">Module</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[vmasanas]	25/10/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Shared Sub AddContent(ByVal nodeModule As XmlNode, ByVal objModule As ModuleInfo)
            Dim xmlattr As XmlAttribute

            If objModule.BusinessControllerClass <> "" And objModule.IsPortable Then
                Try
                    Dim objObject As Object = Framework.Reflection.CreateObject(objModule.BusinessControllerClass, objModule.BusinessControllerClass)
                    If TypeOf objObject Is IPortable Then
                        Dim Content As String = CType(CType(objObject, IPortable).ExportModule(objModule.ModuleID), String)
                        If Content <> "" Then
                            ' add attributes to XML document
                            Dim newnode As XmlNode = nodeModule.OwnerDocument.CreateElement("content")
                            xmlattr = nodeModule.OwnerDocument.CreateAttribute("type")
                            xmlattr.Value = CleanName(objModule.ModuleName)
                            newnode.Attributes.Append(xmlattr)
                            xmlattr = nodeModule.OwnerDocument.CreateAttribute("version")
                            xmlattr.Value = objModule.Version
                            newnode.Attributes.Append(xmlattr)

                            Content = HttpContext.Current.Server.HtmlEncode(Content)
                            newnode.InnerXml = XmlUtils.XMLEncode(Content)

                            nodeModule.AppendChild(newnode)
                        End If
                    End If
                Catch
                    'ignore errors
                End Try
            End If
        End Sub

        Private Shared Function CheckIsInstance(ByVal templateModuleID As Integer, ByVal hModules As Hashtable) As Boolean
            ' will be instance or module?
            Dim IsInstance As Boolean = False
            If templateModuleID > 0 Then
                If Not hModules(templateModuleID) Is Nothing Then
                    ' this module has already been processed -> process as instance
                    IsInstance = True
                End If
            End If

            Return IsInstance
        End Function

        Private Shared Sub CreateEventQueueMessage(ByVal objModule As ModuleInfo, ByVal strContent As String, ByVal strVersion As String, ByVal userID As Integer)
            Dim oAppStartMessage As New EventQueue.EventMessage
            oAppStartMessage.Priority = MessagePriority.High
            oAppStartMessage.ExpirationDate = Now.AddYears(-1)
            oAppStartMessage.SentDate = System.DateTime.Now
            oAppStartMessage.Body = ""
            oAppStartMessage.ProcessorType = "DotNetNuke.Entities.Modules.EventMessageProcessor, DotNetNuke"
            oAppStartMessage.ProcessorCommand = "ImportModule"

            'Add custom Attributes for this message
            oAppStartMessage.Attributes.Add("BusinessControllerClass", objModule.BusinessControllerClass)
            oAppStartMessage.Attributes.Add("ModuleId", objModule.ModuleID.ToString())
            oAppStartMessage.Attributes.Add("Content", strcontent)
            oAppStartMessage.Attributes.Add("Version", strVersion)
            oAppStartMessage.Attributes.Add("UserID", userID.ToString)

            'send it to occur on next App_Start Event
            EventQueueController.SendMessage(oAppStartMessage, "Application_Start")
        End Sub

        Private Shared Function DeserializeModule(ByVal nodeModule As XmlNode, ByVal nodePane As XmlNode, ByVal PortalId As Integer, ByVal TabId As Integer, ByVal ModuleDefId As Integer) As ModuleInfo
            Dim objModules As New ModuleController

            'Create New Module
            Dim objModule As New ModuleInfo
            objModule.PortalID = PortalId
            objModule.TabID = TabId
            objModule.ModuleOrder = -1
            objModule.ModuleTitle = XmlUtils.GetNodeValue(nodeModule, "title")
            objModule.PaneName = XmlUtils.GetNodeValue(nodePane, "name")
            objModule.ModuleDefID = ModuleDefId
            objModule.CacheTime = XmlUtils.GetNodeValueInt(nodeModule, "cachetime")
            objModule.Alignment = XmlUtils.GetNodeValue(nodeModule, "alignment")
            objModule.IconFile = ImportFile(PortalId, XmlUtils.GetNodeValue(nodeModule, "iconfile"))
            objModule.AllTabs = XmlUtils.GetNodeValueBoolean(nodeModule, "alltabs")
            Select Case XmlUtils.GetNodeValue(nodeModule, "visibility")
                Case "Maximized" : objModule.Visibility = VisibilityState.Maximized
                Case "Minimized" : objModule.Visibility = VisibilityState.Minimized
                Case "None" : objModule.Visibility = VisibilityState.None
            End Select
            objModule.Color = XmlUtils.GetNodeValue(nodeModule, "color", "")
            objModule.Border = XmlUtils.GetNodeValue(nodeModule, "border", "")
            objModule.Header = XmlUtils.GetNodeValue(nodeModule, "header", "")
            objModule.Footer = XmlUtils.GetNodeValue(nodeModule, "footer", "")
            objModule.InheritViewPermissions = XmlUtils.GetNodeValueBoolean(nodeModule, "inheritviewpermissions", False)
            objModule.ModulePermissions = New Security.Permissions.ModulePermissionCollection

            objModule.StartDate = XmlUtils.GetNodeValueDate(nodeModule, "startdate", Null.NullDate)
            objModule.EndDate = XmlUtils.GetNodeValueDate(nodeModule, "enddate", Null.NullDate)

            If XmlUtils.GetNodeValue(nodeModule, "containersrc", "") <> "" Then
                objModule.ContainerSrc = XmlUtils.GetNodeValue(nodeModule, "containersrc", "")
            End If
            objModule.DisplayTitle = XmlUtils.GetNodeValueBoolean(nodeModule, "displaytitle", True)
            objModule.DisplayPrint = XmlUtils.GetNodeValueBoolean(nodeModule, "displayprint", True)
            objModule.DisplaySyndicate = XmlUtils.GetNodeValueBoolean(nodeModule, "displaysyndicate", False)

            Return objModule
        End Function

        Private Shared Sub DeserializeModulePermissions(ByVal nodeModulePermissions As XmlNodeList, ByVal PortalId As Integer, ByVal ModuleID As Integer)
            Dim objRoleController As New RoleController
            Dim objRole As RoleInfo
            Dim objModulePermissions As New Security.Permissions.ModulePermissionCollection
            Dim objModulePermissionController As New Security.Permissions.ModulePermissionController
            Dim objPermissionController As New Security.Permissions.PermissionController
            Dim objPermission As Security.Permissions.PermissionInfo
            Dim objModulePermissionCollection As New Security.Permissions.ModulePermissionCollection
            Dim node As XmlNode
            Dim PermissionID As Integer
            Dim arrPermissions As ArrayList
            Dim i As Integer
            Dim PermissionKey, PermissionCode As String
            Dim RoleName As String
            Dim RoleID As Integer
            Dim AllowAccess As Boolean

            For Each node In nodeModulePermissions
                PermissionKey = XmlUtils.GetNodeValue(node, "permissionkey")
                PermissionCode = XmlUtils.GetNodeValue(node, "permissioncode")
                RoleName = XmlUtils.GetNodeValue(node, "rolename")
                AllowAccess = XmlUtils.GetNodeValueBoolean(node, "allowaccess")

                RoleID = Integer.MinValue
                Select Case RoleName
                    Case glbRoleAllUsersName
                        RoleID = Convert.ToInt32(glbRoleAllUsers)
                    Case Common.Globals.glbRoleUnauthUserName
                        RoleID = Convert.ToInt32(glbRoleUnauthUser)
                    Case Else
                        objRole = objRoleController.GetRoleByName(PortalId, RoleName)
                        If Not objRole Is Nothing Then
                            RoleID = objRole.RoleID
                        End If
                End Select
                If RoleID <> Integer.MinValue Then
                    PermissionID = -1
                    arrPermissions = objPermissionController.GetPermissionByCodeAndKey(PermissionCode, PermissionKey)

                    For i = 0 To arrPermissions.Count - 1
                        objPermission = CType(arrPermissions(i), Security.Permissions.PermissionInfo)
                        PermissionID = objPermission.PermissionID
                    Next

                    ' if role was found add, otherwise ignore
                    If PermissionID <> -1 Then
                        Dim objModulePermission As New Security.Permissions.ModulePermissionInfo
                        objModulePermission.ModuleID = ModuleID
                        objModulePermission.PermissionID = PermissionID
                        objModulePermission.RoleID = RoleID
                        objModulePermission.AllowAccess = Convert.ToBoolean(XmlUtils.GetNodeValue(node, "allowaccess"))
                        objModulePermissionController.AddModulePermission(objModulePermission)
                    End If
                End If
            Next

        End Sub

        Private Shared Function FindModule(ByVal nodeModule As XmlNode, ByVal TabId As Integer, ByVal mergeTabs As PortalTemplateModuleAction) As Boolean
            Dim objModules As New ModuleController
            Dim dicModules As Dictionary(Of Integer, ModuleInfo) = objModules.GetTabModules(TabId)
            Dim objModule As ModuleInfo

            Dim moduleFound As Boolean = False
            Dim modTitle As String = XmlUtils.GetNodeValue(nodeModule, "title")
            If mergeTabs = PortalTemplateModuleAction.Merge Then
                For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In dicModules
                    objModule = kvp.Value
                    If modTitle = objModule.ModuleTitle Then
                        moduleFound = True
                        Exit For
                    End If
                Next
            End If

            Return moduleFound
        End Function

        Private Shared Sub GetModuleContent(ByVal nodeModule As XmlNode, ByVal ModuleId As Integer, ByVal TabId As Integer, ByVal PortalId As Integer)
            Dim objModules As New ModuleController
            Dim objModule As ModuleInfo = objModules.GetModule(ModuleId, TabId, True)
            Dim strVersion As String = nodeModule.SelectSingleNode("content").Attributes.ItemOf("version").Value
            Dim strType As String = nodeModule.SelectSingleNode("content").Attributes.ItemOf("type").Value
            Dim strcontent As String = nodeModule.SelectSingleNode("content").InnerXml
            strcontent = strcontent.Substring(9, strcontent.Length - 12)

            If objModule.BusinessControllerClass <> "" And strcontent <> "" Then
                Dim objportal As PortalInfo
                Dim objportals As New PortalController
                objportal = objportals.GetPortal(PortalId)

                'Determine if the Module is copmpletely installed 
                '(ie are we running in the same request that installed the module).
                If objModule.SupportedFeatures = Null.NullInteger Then
                    ' save content in eventqueue for processing after an app restart,
                    ' as modules Supported Features are not updated yet so we
                    ' cannot determine if the module supports IsPortable
                    CreateEventQueueMessage(objModule, strcontent, strVersion, objportal.AdministratorId)
                Else
                    strcontent = HttpContext.Current.Server.HtmlDecode(strcontent)
                    If objModule.IsPortable Then
                        Try
                            Dim objObject As Object = Framework.Reflection.CreateObject(objModule.BusinessControllerClass, objModule.BusinessControllerClass)
                            If TypeOf objObject Is IPortable Then
                                CType(objObject, IPortable).ImportModule(objModule.ModuleID, strcontent, strVersion, objportal.AdministratorId)
                            End If
                        Catch
                            'ignore errors
                        End Try
                    End If
                End If
            End If
        End Sub

        Private Shared Function GetModuleDefinition(ByVal nodeModule As XmlNode) As ModuleDefinitionInfo
            Dim objModuleDefinitions As New ModuleDefinitionController
            Dim objModuleDefinition As ModuleDefinitionInfo = Nothing

            ' Templates prior to v4.3.5 only have the <definition> node to define the Module Type
            ' This <definition> node was populated with the DesktopModuleInfo.ModuleName property
            ' Thus there is no mechanism to determine to which module definition the module belongs.
            '
            ' Template from v4.3.5 on also have the <moduledefinition> element that is populated
            ' with the ModuleDefinitionInfo.FriendlyName.  Therefore the module Instance identifies
            ' which Module Definition it belongs to.

            'Get the DesktopModule defined by the <definition> element
            Dim objDesktopModule As DesktopModuleInfo = GetDesktopModuleByName(XmlUtils.GetNodeValue(nodeModule, "definition"))
            If Not objDesktopModule Is Nothing Then

                'Get the moduleDefinition from the <moduledefinition> element
                Dim friendlyName As String = XmlUtils.GetNodeValue(nodeModule, "moduledefinition")

                If String.IsNullOrEmpty(friendlyName) Then
                    'Module is pre 4.3.5 so get the first Module Definition (at least it won't throw an error then)
                    Dim arrModuleDefinitions As ArrayList = objModuleDefinitions.GetModuleDefinitions(objDesktopModule.DesktopModuleID)
                    objModuleDefinition = CType(arrModuleDefinitions(0), ModuleDefinitionInfo)
                Else
                    'Module is 4.3.5 or later so get the Module Defeinition by its friendly name
                    objModuleDefinition = objModuleDefinitions.GetModuleDefinitionByName(objDesktopModule.DesktopModuleID, friendlyName)
                End If
            End If

            Return objModuleDefinition
        End Function


#Region "Public Shared Methods"

        Public Shared Function CacheDirectory() As String
            Return PortalController.GetCurrentPortalSettings.HomeDirectoryMapPath & "Cache"
        End Function

        Public Shared Function CacheFileName(ByVal TabModuleID As Integer) As String
            Return CacheDirectory & "\" & CleanFileName(CacheKey(TabModuleID)) & ".resources"
        End Function

        Public Shared Function CacheKey(ByVal TabModuleID As Integer) As String
            Dim strCacheKey As String = "TabModule:"
            strCacheKey += TabModuleID.ToString & ":"
            strCacheKey += System.Threading.Thread.CurrentThread.CurrentCulture.ToString
            Return strCacheKey
        End Function

        Public Shared Function DeserializeModule(ByVal nodeModule As XmlNode, ByVal nodePane As XmlNode, ByVal PortalId As Integer, ByVal TabId As Integer, ByVal mergeTabs As PortalTemplateModuleAction, ByVal hModules As Hashtable) As ModuleInfo
            Dim objModules As New ModuleController
            Dim objModuleDefinition As ModuleDefinitionInfo = GetModuleDefinition(nodeModule)
            Dim objModule As ModuleInfo
            Dim intModuleId As Integer

            ' will be instance or module?
            Dim templateModuleID As Integer = XmlUtils.GetNodeValueInt(nodeModule, "moduleID")
            Dim IsInstance As Boolean = CheckIsInstance(templateModuleID, hModules)

            If Not objModuleDefinition Is Nothing Then
                'If Mode is Merge Check if Module exists
                If Not FindModule(nodeModule, TabId, mergeTabs) Then
                    objModule = DeserializeModule(nodeModule, nodePane, PortalId, TabId, objModuleDefinition.ModuleDefID)

                    If Not IsInstance Then
                        'Add new module
                        intModuleId = objModules.AddModule(objModule)
                        If templateModuleID > 0 Then
                            hModules.Add(templateModuleID, intModuleId)
                        End If
                    Else
                        'Add instance
                        objModule.ModuleID = Convert.ToInt32(hModules(templateModuleID))
                        intModuleId = objModules.AddModule(objModule)
                    End If

                    If XmlUtils.GetNodeValue(nodeModule, "content") <> "" And Not IsInstance Then
                        GetModuleContent(nodeModule, intModuleId, TabId, PortalId)
                    End If

                    ' Process permissions only once
                    If Not IsInstance Then
                        Dim nodeModulePermissions As XmlNodeList = nodeModule.SelectNodes("modulepermissions/permission")
                        DeserializeModulePermissions(nodeModulePermissions, PortalId, intModuleId)
                    End If
                End If
            End If
        End Function

        ''' <summary>
        ''' SerializeModule
        ''' </summary>
        ''' <param name="xmlModule">The Xml Document to use for the Module</param>
        ''' <param name="objModule">The ModuleInfo object to serialize</param>
        ''' <param name="includeContent">A flak that determines whether the content of the module is serialised.</param>
        Public Shared Function SerializeModule(ByVal xmlModule As XmlDocument, ByVal objModule As ModuleInfo, ByVal includeContent As Boolean) As XmlNode
            Dim xserModules As New XmlSerializer(GetType(ModuleInfo))
            Dim nodeModule, nodeDefinition, newnode As XmlNode

            Dim objmodules As New ModuleController
            Dim objDesktopModules As New DesktopModuleController
            Dim objModuleDefController As New ModuleDefinitionController

            Dim sw As New StringWriter
            xserModules.Serialize(sw, objModule)

            xmlModule.LoadXml(sw.GetStringBuilder().ToString())
            nodeModule = xmlModule.SelectSingleNode("module")
            nodeModule.Attributes.Remove(nodeModule.Attributes.ItemOf("xmlns:xsd"))
            nodeModule.Attributes.Remove(nodeModule.Attributes.ItemOf("xmlns:xsi"))

            'remove unwanted elements
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("portalid"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("tabid"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("tabmoduleid"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("moduledefid"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("moduleorder"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("panename"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("isdeleted"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("desktopmoduleid"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("friendlyname"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("description"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("version"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("ispremium"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("isadmin"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("businesscontrollerclass"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("supportedfeatures"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("modulecontrolid"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("controlsrc"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("controltype"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("controltitle"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("helpurl"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("authorizededitroles"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("authorizedviewroles"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("foldername"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("modulename"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("compatibleversions"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("dependencies"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("permissions"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("defaultcachetime"))
            nodeModule.RemoveChild(nodeModule.SelectSingleNode("supportspartialrendering"))

            For Each nodePermission As XmlNode In nodeModule.SelectNodes("modulepermissions/permission")
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("modulepermissionid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("permissionid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("moduleid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("roleid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("userid"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("username"))
                nodePermission.RemoveChild(nodePermission.SelectSingleNode("displayname"))
            Next

            If includeContent Then
                AddContent(nodeModule, objModule)
            End If

            newnode = xmlModule.CreateElement("definition")

            Dim objModuleDef As ModuleDefinitionInfo = objModuleDefController.GetModuleDefinition(objModule.ModuleDefID)
            newnode.InnerText = objDesktopModules.GetDesktopModule(objModuleDef.DesktopModuleID).ModuleName
            nodeModule.AppendChild(newnode)

            'Add Module Definition Info
            nodeDefinition = xmlModule.CreateElement("moduledefinition")
            nodeDefinition.InnerText = objModuleDef.FriendlyName
            nodeModule.AppendChild(nodeDefinition)

            Return nodeModule
        End Function

        Public Shared Sub SynchronizeModule(ByVal moduleID As Integer)
            Dim cacheMethod As String = Convert.ToString(Common.Globals.HostSettings("ModuleCaching"))
            Dim objModules As New ModuleController
            Dim arrModules As ArrayList = objModules.GetModuleTabs(moduleID)
            For Each objModule As ModuleInfo In arrModules
                If cacheMethod <> "D" Then
                    DataCache.RemoveCache(CacheKey(objModule.TabModuleID))
                Else ' disk caching
                    If System.IO.File.Exists(CacheFileName(objModule.TabModuleID)) Then
                        Try
                            System.IO.File.Delete(CacheFileName(objModule.TabModuleID))
                        Catch
                            ' error deleting file from disk
                        End Try
                    End If
                End If
            Next
        End Sub

#End Region

#Region "Public Methods"

        ''' <summary>
        ''' add a module to a page
        ''' </summary>
        ''' <param name="objModule">moduleInfo for the module to create</param>
        ''' <returns>ID of the created module</returns>
        ''' <history>
        '''    [sleupold] 2007-09-24 documented
        ''' </history>
        Public Function AddModule(ByVal objModule As ModuleInfo) As Integer
            ' add module
            If Null.IsNull(objModule.ModuleID) Then
                objModule.ModuleID = DataProvider.Instance().AddModule(objModule.PortalID, objModule.ModuleDefID, objModule.ModuleTitle, objModule.AllTabs, objModule.Header, objModule.Footer, objModule.StartDate, objModule.EndDate, objModule.InheritViewPermissions, objModule.IsDeleted)
                ' set module permissions
                If Not objModule.ModulePermissions Is Nothing Then
                    Dim objModulePermissionController As New Security.Permissions.ModulePermissionController
                    Dim objModulePermissions As Security.Permissions.ModulePermissionCollection
                    objModulePermissions = objModule.ModulePermissions
                    Dim objModulePermission As New Security.Permissions.ModulePermissionInfo
                    For Each objModulePermission In objModulePermissions
                        objModulePermission.ModuleID = objModule.ModuleID
                        objModulePermissionController.AddModulePermission(objModulePermission, objModule.TabID)
                    Next
                End If
            End If

            'This will fail if the page already contains this module
            Try
                ' add tabmodule
                DataProvider.Instance().AddTabModule(objModule.TabID, objModule.ModuleID, objModule.ModuleOrder, objModule.PaneName, objModule.CacheTime, objModule.Alignment, objModule.Color, objModule.Border, objModule.IconFile, objModule.Visibility, objModule.ContainerSrc, objModule.DisplayTitle, objModule.DisplayPrint, objModule.DisplaySyndicate)

                If objModule.ModuleOrder = -1 Then
                    ' position module at bottom of pane
                    UpdateModuleOrder(objModule.TabID, objModule.ModuleID, objModule.ModuleOrder, objModule.PaneName)
                Else
                    ' position module in pane
                    UpdateTabModuleOrder(objModule.TabID, objModule.PortalID)
                End If
            Catch
                ' module already in the page, ignore error
            End Try

            ClearCache(objModule.TabID)

            Return objModule.ModuleID

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CopyModule copies a Module from one Tab to another optionally including all the 
        '''	TabModule settings
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="moduleId">The Id of the module to copy</param>
        '''	<param name="fromTabId">The Id of the source tab</param>
        '''	<param name="toTabId">The Id of the destination tab</param>
        '''	<param name="toPaneName">The name of the Pane on the destination tab where the module will end up</param>
        '''	<param name="includeSettings">A flag to indicate whether the settings are copied to the new Tab</param>
        ''' <history>
        ''' 	[cnurse]	10/21/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub CopyModule(ByVal moduleId As Integer, ByVal fromTabId As Integer, ByVal toTabId As Integer, ByVal toPaneName As String, ByVal includeSettings As Boolean)
            'First Get the Module itself
            Dim objModule As ModuleInfo = GetModule(moduleId, fromTabId, False)

            'If the Destination Pane Name is not set, assume the same name as the source
            If toPaneName = "" Then
                toPaneName = objModule.PaneName
            End If

            'This will fail if the page already contains this module
            Try
                'Add a copy of the module to the bottom of the Pane for the new Tab
                DataProvider.Instance().AddTabModule(toTabId, moduleId, -1, toPaneName, objModule.CacheTime, objModule.Alignment, objModule.Color, objModule.Border, objModule.IconFile, objModule.Visibility, objModule.ContainerSrc, objModule.DisplayTitle, objModule.DisplayPrint, objModule.DisplaySyndicate)

                'Optionally copy the TabModuleSettings
                If includeSettings Then
                    Dim toModule As ModuleInfo = GetModule(moduleId, toTabId, False)
                    CopyTabModuleSettings(objModule, toModule)
                End If
            Catch
                ' module already in the page, ignore error
            End Try

            ClearCache(fromTabId)
            ClearCache(toTabId)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CopyModule copies a Module from one Tab to a collection of Tabs optionally
        '''	including the TabModule settings
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="moduleId">The Id of the module to copy</param>
        '''	<param name="fromTabId">The Id of the source tab</param>
        '''	<param name="toTabs">An ArrayList of TabItem objects</param>
        '''	<param name="includeSettings">A flag to indicate whether the settings are copied to the new Tab</param>
        ''' <history>
        ''' 	[cnurse]	2004-10-22	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub CopyModule(ByVal moduleId As Integer, ByVal fromTabId As Integer, ByVal toTabs As ArrayList, ByVal includeSettings As Boolean)
            'Iterate through collection copying the module to each Tab (except the source)
            For Each objTab As TabInfo In toTabs
                If objTab.TabID <> fromTabId Then
                    CopyModule(moduleId, fromTabId, objTab.TabID, "", includeSettings)
                End If
            Next
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CopyTabModuleSettings copies the TabModuleSettings from one instance to another
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="fromModule">The module to copy from</param>
        '''	<param name="toModule">The module to copy to</param>
        ''' <history>
        ''' 	[cnurse]	2005-01-11	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub CopyTabModuleSettings(ByVal fromModule As ModuleInfo, ByVal toModule As ModuleInfo)

            'Get the TabModuleSettings
            Dim settings As Hashtable = GetTabModuleSettings(fromModule.TabModuleID)

            'Copy each setting to the new TabModule instance
            For Each setting As DictionaryEntry In settings
                UpdateTabModuleSetting(toModule.TabModuleID, CType(setting.Key, String), CType(setting.Value, String))
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DeleteAllModules deletes all instaces of a Module (from a collection), optionally excluding the
        '''	current instance, and optionally including deleting the Module itself.
        ''' </summary>
        ''' <remarks>
        '''	Note - the base module is not removed unless both the flags are set, indicating
        '''	to delete all instances AND to delete the Base Module
        ''' </remarks>
        '''	<param name="moduleId">The Id of the module to copy</param>
        '''	<param name="tabId">The Id of the current tab</param>
        '''	<param name="fromTabs">An ArrayList of TabItem objects</param>
        '''	<param name="includeCurrent">A flag to indicate whether to delete from the current tab
        '''		as identified ny tabId</param>
        '''	<param name="deleteBaseModule">A flag to indicate whether to delete the Module itself</param>
        ''' <history>
        ''' 	[cnurse]	2004-10-22	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub DeleteAllModules(ByVal moduleId As Integer, ByVal tabId As Integer, ByVal fromTabs As ArrayList, ByVal includeCurrent As Boolean, ByVal deleteBaseModule As Boolean)
            'Iterate through collection deleting the module from each Tab (except the current)
            For Each objTab As TabInfo In fromTabs
                If objTab.TabID <> tabId Or includeCurrent Then
                    DeleteTabModule(objTab.TabID, moduleId)
                End If
            Next

            'Optionally delete the Module
            If includeCurrent And deleteBaseModule Then
                DeleteModule(moduleId)
            End If

            ClearCache(tabId)

        End Sub

        ''' <summary>
        ''' Delete a module instance permanently from the database
        ''' </summary>
        ''' <param name="ModuleId">ID of the module instance</param>
        ''' <history>
        '''    [sleupold]   1007-09-24 documented
        ''' </history>
        Public Sub DeleteModule(ByVal ModuleId As Integer)

            DataProvider.Instance().DeleteModule(ModuleId)

            'Delete Search Items for this Module
            DataProvider.Instance().DeleteSearchItems(ModuleId)

        End Sub

        ''' <summary>
        ''' Delete a module reference permanently from the database.
        ''' if there are no other references, the module instance is deleted as well
        ''' </summary>
        ''' <param name="TabId">ID of the page</param>
        ''' <param name="ModuleId">ID of the module instance</param>
        ''' <history>
        '''    [sleupold]   1007-09-24 documented
        ''' </history>
        Public Sub DeleteTabModule(ByVal TabId As Integer, ByVal ModuleId As Integer)
            ' save moduleinfo
            Dim objModule As ModuleInfo = GetModule(ModuleId, TabId, False)

            If Not objModule Is Nothing Then
                ' delete the module instance for the tab
                DataProvider.Instance().DeleteTabModule(TabId, ModuleId)

                ' reorder all modules on tab
                UpdateTabModuleOrder(TabId, objModule.PortalID)

                ' check if all modules instances have been deleted
                If GetModule(ModuleId, Null.NullInteger, True).TabID = Null.NullInteger Then
                    ' soft delete the module
                    objModule.TabID = Null.NullInteger
                    objModule.IsDeleted = True
                    UpdateModule(objModule)

                    'Delete Search Items for this Module
                    DataProvider.Instance().DeleteSearchItems(ModuleId)
                End If
            End If

            ClearCache(TabId)
        End Sub

        ''' <summary>
        ''' get info of all modules in any portal of the installation
        ''' </summary>
        ''' <returns>moduleInfo of all modules</returns>
        ''' <remarks>created for upgrade purposes</remarks>
        ''' <history>
        '''    [sleupold] 2007-09-24 documented
        '''</history>
        Public Function GetAllModules() As ArrayList
            Return FillModuleInfoCollection(DataProvider.Instance().GetAllModules(), True, True)
        End Function

        ''' <summary>
        ''' get a Module object
        ''' </summary>
        ''' <param name="ModuleId">ID of the module</param>
        ''' <param name="TabId">ID of the page</param>
        ''' <param name="ignoreCache">flag, if data shall not be taken from cache</param>
        ''' <returns>ArrayList of ModuleInfo objects</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetModule(ByVal ModuleId As Integer, ByVal TabId As Integer, ByVal ignoreCache As Boolean) As ModuleInfo
            Dim modInfo As ModuleInfo = Nothing
            Dim bFound As Boolean = False
            If Not ignoreCache Then
                'First try the cache
                Dim dicModules As Dictionary(Of Integer, ModuleInfo) = GetTabModules(TabId)
                bFound = dicModules.TryGetValue(ModuleId, modInfo)
            End If

            If ignoreCache Or Not bFound Then
                Dim dr As IDataReader = DataProvider.Instance().GetModule(ModuleId, TabId)
                Try
                    modInfo = FillModuleInfo(dr)
                Finally
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try
            End If
            Return modInfo
        End Function

        ''' <summary>
        ''' get all Module objects of a portal
        ''' </summary>
        ''' <param name="PortalID">ID of the portal</param>
        ''' <returns>ArrayList of ModuleInfo objects</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetModules(ByVal PortalID As Integer) As ArrayList
            Return FillModuleInfoCollection(DataProvider.Instance().GetModules(PortalID))
        End Function

        ''' <summary>
        ''' get all Module objects of a portal, optionally including permissions
        ''' </summary>
        ''' <param name="PortalID">ID of the portal</param>
        ''' <param name="IncludePermissions">specify, whether permissions shall be hydrated</param>
        ''' <returns>ArrayList of ModuleInfo objects</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetModules(ByVal PortalID As Integer, ByVal IncludePermissions As Boolean) As ArrayList
            Return FillModuleInfoCollection(DataProvider.Instance().GetModules(PortalID), IncludePermissions, False)
        End Function

        ''' <summary>
        ''' get Module objects of a portal, either only those, to be placed on all tabs or not
        ''' </summary>
        ''' <param name="PortalID">ID of the portal</param>
        ''' <param name="AllTabs">specify, whether to return modules to be shown on all tabs or those to be shown on specified tabs</param>
        ''' <returns>ArrayList of TabModuleInfo objects</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetAllTabsModules(ByVal PortalID As Integer, ByVal AllTabs As Boolean) As ArrayList
            Return FillModuleInfoCollection(DataProvider.Instance().GetAllTabsModules(PortalID, AllTabs))
        End Function

        ''' <summary>
        ''' Get ModuleInfo object of first module instance with a given friendly name of the module definition
        ''' </summary>
        ''' <param name="PortalId">ID of the portal, where to look for the module</param>
        ''' <param name="FriendlyName">friendly name of module definition</param>
        ''' <returns>ModuleInfo of first module instance</returns>
        ''' <remarks>preferably used for admin and host modules</remarks>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetModuleByDefinition(ByVal PortalId As Integer, ByVal FriendlyName As String) As ModuleInfo

            ' declare return object
            Dim objModule As ModuleInfo = Nothing

            ' format cache key
            Dim key As String = String.Format(DataCache.ModuleCacheKey, PortalId)

            ' get module dictionary from cache
            Dim modules As Dictionary(Of String, ModuleInfo) = TryCast(DataCache.GetPersistentCacheItem(key, GetType(Dictionary(Of String, ModuleInfo))), Dictionary(Of String, ModuleInfo))

            If modules Is Nothing Then
                ' create new dictionary
                modules = New Dictionary(Of String, ModuleInfo)
            End If

            ' return module if it exists
            If modules.ContainsKey(FriendlyName) Then
                objModule = modules(FriendlyName)
            Else
                ' clone the dictionary so that we have a local copy
                Dim clonemodules As Dictionary(Of String, ModuleInfo) = New Dictionary(Of String, ModuleInfo)
                For Each objModule In modules.Values
                    clonemodules(objModule.FriendlyName) = objModule
                Next

                ' get from database
                Dim dr As IDataReader = DataProvider.Instance().GetModuleByDefinition(PortalId, FriendlyName)
                Try
                    ' hydrate object
                    objModule = FillModuleInfo(dr)
                Finally
                    ' close connection
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try

                If objModule IsNot Nothing Then
                    ' add the module to the dictionary
                    clonemodules(objModule.FriendlyName) = objModule

                    ' set module caching settings
                    Dim timeOut As Int32 = DataCache.ModuleCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                    ' cache module dictionary
                    If timeOut > 0 Then
                        DataCache.SetCache(key, clonemodules, TimeSpan.FromMinutes(timeOut), True)
                    End If
                End If
            End If

            ' return module object
            Return objModule
        End Function

        ''' <summary>
        ''' Get a list of all Module Instances
        ''' </summary>
        ''' <param name="PortalId">ID of the portal to look at</param>
        ''' <param name="FriendlyName">friendly Name of Module Definition</param>
        ''' <returns>ArrayList of ModuleINfo objects</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetModulesByDefinition(ByVal PortalId As Integer, ByVal FriendlyName As String) As ArrayList
            Return FillModuleInfoCollection(DataProvider.Instance().GetModuleByDefinition(PortalId, FriendlyName))
        End Function

        ''' <summary>
        ''' For a portal get a list of all active module and tabmodule references that support iSearchable
        ''' </summary>
        ''' <param name="PortalId">ID of the portal to be searched</param>
        ''' <returns>Arraylist of ModuleInfo for modules supporting search.</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetSearchModules(ByVal PortalId As Integer) As ArrayList
            Return FillModuleInfoCollection(DataProvider.Instance().GetSearchModules(PortalId))
        End Function

        ''' <summary>
        ''' Get all Module references on a tab
        ''' </summary>
        ''' <param name="TabId"></param>
        ''' <returns>Dictionary of ModuleID and ModuleInfo</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Function GetTabModules(ByVal TabId As Integer) As Dictionary(Of Integer, ModuleInfo)
            Dim key As String = String.Format(DataCache.TabModuleCacheKey, TabId)

            'First Check the Tab Cache
            Dim modules As Dictionary(Of Integer, ModuleInfo) = TryCast(DataCache.GetPersistentCacheItem(key, GetType(Dictionary(Of Integer, ModuleInfo))), Dictionary(Of Integer, ModuleInfo))

            If modules Is Nothing Then
                'tabmodule caching settings
                Dim timeOut As Int32 = DataCache.TabModuleCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get modules form Database
                modules = FillModuleInfoDictionary(DataProvider.Instance().GetTabModules(TabId))

                'Cache tabs
                If timeOut > 0 Then
                    DataCache.SetCache(key, modules, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If
            Return modules
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' MoveModule moes a Module from one Tab to another including all the 
        '''	TabModule settings
        ''' </summary>
        '''	<param name="moduleId">The Id of the module to move</param>
        '''	<param name="fromTabId">The Id of the source tab</param>
        '''	<param name="toTabId">The Id of the destination tab</param>
        '''	<param name="toPaneName">The name of the Pane on the destination tab where the module will end up</param>
        ''' <history>
        ''' 	[cnurse]	10/21/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub MoveModule(ByVal moduleId As Integer, ByVal fromTabId As Integer, ByVal toTabId As Integer, ByVal toPaneName As String)

            'First copy the Module to the new Tab (including the TabModuleSettings)
            CopyModule(moduleId, fromTabId, toTabId, toPaneName, True)

            'Next Remove the Module from the source tab
            DeleteTabModule(fromTabId, moduleId)

        End Sub

        ''' <summary>
        ''' Update module settings and permissions in database from ModuleInfo
        ''' </summary>
        ''' <param name="objModule">ModuleInfo of the module to update</param>
        ''' <history>
        '''    [sleupold]   2007-09-24 commented
        ''' </history>
        Public Sub UpdateModule(ByVal objModule As ModuleInfo)
            ' update module
            DataProvider.Instance().UpdateModule(objModule.ModuleID, objModule.ModuleTitle, objModule.AllTabs, objModule.Header, objModule.Footer, objModule.StartDate, objModule.EndDate, objModule.InheritViewPermissions, objModule.IsDeleted)

            ' update module permissions
            Dim objModulePermissionController As New Security.Permissions.ModulePermissionController
            Dim objCurrentModulePermissions As Security.Permissions.ModulePermissionCollection
            objCurrentModulePermissions = objModulePermissionController.GetModulePermissionsCollectionByModuleID(objModule.ModuleID, objModule.TabID)
            If Not objCurrentModulePermissions.CompareTo(objModule.ModulePermissions) Then
                objModulePermissionController.DeleteModulePermissionsByModuleID(objModule.ModuleID)
                Dim objModulePermission As Security.Permissions.ModulePermissionInfo
                For Each objModulePermission In objModule.ModulePermissions
                    objModulePermission.ModuleID = objModule.ModuleID
                    If objModule.InheritViewPermissions And objModulePermission.PermissionKey = "VIEW" Then
                        objModulePermissionController.DeleteModulePermission(objModulePermission.ModulePermissionID)
                    Else
                        If objModulePermission.AllowAccess Then
                            objModulePermissionController.AddModulePermission(objModulePermission, objModule.TabID)
                        End If
                    End If
                Next
            End If

            If Not Null.IsNull(objModule.TabID) Then

                ' update tabmodule
                DataProvider.Instance().UpdateTabModule(objModule.TabID, objModule.ModuleID, objModule.ModuleOrder, objModule.PaneName, objModule.CacheTime, objModule.Alignment, objModule.Color, objModule.Border, objModule.IconFile, objModule.Visibility, objModule.ContainerSrc, objModule.DisplayTitle, objModule.DisplayPrint, objModule.DisplaySyndicate)

                ' update module order in pane
                UpdateModuleOrder(objModule.TabID, objModule.ModuleID, objModule.ModuleOrder, objModule.PaneName)

                ' set the default module
                If objModule.IsDefaultModule Then
                    PortalSettings.UpdateSiteSetting(objModule.PortalID, "defaultmoduleid", objModule.ModuleID.ToString)
                    PortalSettings.UpdateSiteSetting(objModule.PortalID, "defaulttabid", objModule.TabID.ToString)
                End If

                ' apply settings to all desktop modules in portal
                If objModule.AllModules Then
                    Dim objTabs As New TabController
                    For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(objModule.PortalID)
                        Dim objTab As TabInfo = tabPair.Value
                        If Not objTab.IsAdminTab Then
                            For Each modulePair As KeyValuePair(Of Integer, ModuleInfo) In GetTabModules(objTab.TabID)
                                Dim objTargetModule As ModuleInfo = modulePair.Value
                                DataProvider.Instance().UpdateTabModule(objTargetModule.TabID, objTargetModule.ModuleID, objTargetModule.ModuleOrder, objTargetModule.PaneName, objTargetModule.CacheTime, objModule.Alignment, objModule.Color, objModule.Border, objModule.IconFile, objModule.Visibility, objModule.ContainerSrc, objModule.DisplayTitle, objModule.DisplayPrint, objModule.DisplaySyndicate)
                            Next
                        End If
                    Next
                End If
            End If

            ClearCache(objModule.TabID)
        End Sub

        ''' <summary>
        ''' set/change the module position within a pane on a page
        ''' </summary>
        ''' <param name="TabId">ID of the page</param>
        ''' <param name="ModuleId">ID of the module on the page</param>
        ''' <param name="ModuleOrder">position within the controls list on page, -1 if to be added at the end</param>
        ''' <param name="PaneName">name of the pane, the module is placed in on the page</param>
        ''' <history>
        '''    [sleupold] 2007-09-24 commented
        ''' </history>
        Public Sub UpdateModuleOrder(ByVal TabId As Integer, ByVal ModuleId As Integer, ByVal ModuleOrder As Integer, ByVal PaneName As String)
            Dim objModule As ModuleInfo = GetModule(ModuleId, TabId, False)
            If Not objModule Is Nothing Then
                ' adding a module to a new pane - places the module at the bottom of the pane 
                If ModuleOrder = -1 Then
                    Dim dr As IDataReader = DataProvider.Instance().GetTabModuleOrder(TabId, PaneName)
                    While dr.Read
                        ModuleOrder = Convert.ToInt32(dr("ModuleOrder"))
                    End While
                    dr.Close()
                    ModuleOrder += 2
                End If

                DataProvider.Instance().UpdateModuleOrder(TabId, ModuleId, ModuleOrder, PaneName)

                ' clear cache
                If objModule.AllTabs = False Then
                    ClearCache(TabId)
                Else
                    Dim objTabs As New TabController
                    For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(objModule.PortalID)
                        Dim objTab As TabInfo = tabPair.Value
                        ClearCache(objTab.TabID)
                    Next
                End If
            End If

        End Sub

        ''' <summary>
        ''' set/change all module's positions within a page
        ''' </summary>
        ''' <param name="PortalId">Portal the page is in</param>
        ''' <param name="TabId">ID of the page</param>
        ''' <history>
        '''    [sleupold]   2007-09-24 documented
        ''' </history>
        Public Sub UpdateTabModuleOrder(ByVal TabId As Integer, ByVal PortalId As Integer)
            Dim ModuleCounter As Integer
            Dim dr As IDataReader = DataProvider.Instance().GetTabPanes(TabId)
            While dr.Read
                ModuleCounter = 0
                Dim dr2 As IDataReader = DataProvider.Instance().GetTabModuleOrder(TabId, Convert.ToString(dr("PaneName")))
                While dr2.Read
                    ModuleCounter += 1
                    DataProvider.Instance().UpdateModuleOrder(TabId, Convert.ToInt32(dr2("ModuleID")), (ModuleCounter * 2) - 1, Convert.ToString(dr("PaneName")))
                End While
                dr2.Close()
            End While
            dr.Close()

            'clear module cache
            ClearCache(TabId)
        End Sub

        ''' <summary>
        ''' Get a list of all TabModule references of a module instance
        ''' </summary>
        ''' <param name="ModuleID">ID of the Module</param>
        ''' <returns>ArrayList of ModuleInfo</returns>
        ''' <history>
        '''    [sleupold]   2007-09-24 documented
        ''' </history>
        Public Function GetModuleTabs(ByVal ModuleID As Integer) As ArrayList
            Return FillModuleInfoCollection(DataProvider.Instance().GetModule(ModuleID, Null.NullInteger))
        End Function

#Region "ModuleSettings"
        ''' <summary>
        ''' read all settings for a module from ModuleSettings table
        ''' </summary>
        ''' <param name="ModuleId">ID of the module</param>
        ''' <returns>(cached) hashtable containing all settings</returns>
        ''' <remarks>TabModuleSettings are not included</remarks>
        ''' <history>
        '''    [sleupold] 2007-09-24 commented
        ''' </history>
        Public Function GetModuleSettings(ByVal ModuleId As Integer) As Hashtable

            Dim objSettings As Hashtable

            Dim strCacheKey As String = "GetModuleSettings" & ModuleId.ToString

            objSettings = CType(DataCache.GetCache(strCacheKey), Hashtable)

            If objSettings Is Nothing Then

                objSettings = New Hashtable

                Dim dr As IDataReader = DataProvider.Instance().GetModuleSettings(ModuleId)

                While dr.Read()

                    If Not dr.IsDBNull(1) Then
                        objSettings(dr.GetString(0)) = dr.GetString(1)
                    Else
                        objSettings(dr.GetString(0)) = ""
                    End If

                End While

                dr.Close()

                ' cache data
                Dim intCacheTimeout As Integer = 20 * Convert.ToInt32(Common.Globals.PerformanceSetting)
                DataCache.SetCache(strCacheKey, objSettings, TimeSpan.FromMinutes(intCacheTimeout))

            End If

            Return objSettings

        End Function

        ''' <summary>
        ''' Adds or updates a module's setting value
        ''' </summary>
        ''' <param name="ModuleId">ID of the module, the setting belongs to</param>
        ''' <param name="SettingName">name of the setting property</param>
        ''' <param name="SettingValue">value of the setting (String).</param>
        ''' <remarks>empty SettingValue will remove the setting, if not preserveIfEmpty is true</remarks>
        ''' <history>
        '''    [sleupold] 2007-09-24 added removal for empty settings
        ''' </history>
        Public Sub UpdateModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)

            Dim dr As IDataReader = DataProvider.Instance().GetModuleSetting(ModuleId, SettingName)

            If dr.Read Then
                DataProvider.Instance().UpdateModuleSetting(ModuleId, SettingName, SettingValue)
            Else
                DataProvider.Instance().AddModuleSetting(ModuleId, SettingName, SettingValue)
            End If
            dr.Close()

            DataCache.RemoveCache("GetModuleSettings" & ModuleId.ToString)

        End Sub

        ''' <summary>
        ''' Delete a Setting of a module instance
        ''' </summary>
        ''' <param name="ModuleId">ID of the affected module</param>
        ''' <param name="SettingName">Name of the setting to be deleted</param>
        ''' <history>
        '''    [sleupold] 2007-09-24 documented
        ''' </history>
        Public Sub DeleteModuleSetting(ByVal ModuleId As Integer, ByVal SettingName As String)
            DataProvider.Instance().DeleteModuleSetting(ModuleId, SettingName)
            DataCache.RemoveCache("GetModuleSettings" & ModuleId.ToString)
        End Sub

        ''' <summary>
        ''' Delete all Settings of a module instance
        ''' </summary>
        ''' <param name="ModuleId">ID of the affected module</param>
        ''' <history>
        '''    [sleupold] 2007-09-24 documented
        ''' </history>
        Public Sub DeleteModuleSettings(ByVal ModuleId As Integer)
            DataProvider.Instance().DeleteModuleSettings(ModuleId)
            DataCache.RemoveCache("GetModuleSettings" & ModuleId.ToString)
        End Sub

#End Region

#Region "TabModuleSettings"

        ''' <summary>
        ''' read all settings for a module on a page from TabModuleSettings Table
        ''' </summary>
        ''' <param name="TabModuleId">ID of the tabModule</param>
        ''' <returns>(cached) hashtable containing all settings</returns>
        ''' <remarks>ModuleSettings are not included</remarks>
        ''' <history>
        '''    [sleupold] 2007-09-24 documented
        ''' </history>
        Public Function GetTabModuleSettings(ByVal TabModuleId As Integer) As Hashtable

            Dim strCacheKey As String = "GetTabModuleSettings" & TabModuleId.ToString
            Dim objSettings As Hashtable = CType(DataCache.GetCache(strCacheKey), Hashtable)

            If objSettings Is Nothing Then
                objSettings = New Hashtable
                Dim dr As IDataReader = DataProvider.Instance().GetTabModuleSettings(TabModuleId)

                While dr.Read()
                    If Not dr.IsDBNull(1) Then
                        objSettings(dr.GetString(0)) = dr.GetString(1)
                    Else
                        objSettings(dr.GetString(0)) = String.Empty
                    End If
                End While
                dr.Close()

                ' cache data
                Dim intCacheTimeout As Integer = 20 * Convert.ToInt32(Common.Globals.PerformanceSetting)
                DataCache.SetCache(strCacheKey, objSettings, TimeSpan.FromMinutes(intCacheTimeout))
            End If

            Return objSettings

        End Function

        ''' <summary>
        ''' Adds or updates a module's setting value
        ''' </summary>
        ''' <param name="TabModuleId">ID of the tabmodule, the setting belongs to</param>
        ''' <param name="SettingName">name of the setting property</param>
        ''' <param name="SettingValue">value of the setting (String).</param>
        ''' <remarks>empty SettingValue will relove the setting</remarks>
        ''' <history>
        '''    [sleupold] 2007-09-24 added removal for empty settings
        ''' </history>
        Public Sub UpdateTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String, ByVal SettingValue As String)

            Dim dr As IDataReader = DataProvider.Instance().GetTabModuleSetting(TabModuleId, SettingName)

            If dr.Read Then
                DataProvider.Instance().UpdateTabModuleSetting(TabModuleId, SettingName, SettingValue)
            Else
                DataProvider.Instance().AddTabModuleSetting(TabModuleId, SettingName, SettingValue)
            End If
            dr.Close()

            DataCache.RemoveCache("GetTabModuleSettings" & TabModuleId.ToString)
        End Sub

        ''' <summary>
        ''' Delete a specific setting of a tabmodule reference
        ''' </summary>
        ''' <param name="TabModuleId">ID of the affected tabmodule</param>
        ''' <param name="SettingName">Name of the setting to remove</param>
        ''' <history>
        '''    [sleupold] 2007-09-24 documented
        ''' </history>
        Public Sub DeleteTabModuleSetting(ByVal TabModuleId As Integer, ByVal SettingName As String)
            DataProvider.Instance().DeleteTabModuleSetting(TabModuleId, SettingName)
            DataCache.RemoveCache("GetTabModuleSettings" & TabModuleId.ToString)
        End Sub

        ''' <summary>
        ''' Delete all settings of a tabmodule reference
        ''' </summary>
        ''' <param name="TabModuleId">ID of the affected tabmodule</param>
        ''' <history>
        '''    [sleupold] 2007-09-24 documented
        ''' </history>
        Public Sub DeleteTabModuleSettings(ByVal TabModuleId As Integer)
            DataProvider.Instance().DeleteTabModuleSettings(TabModuleId)
            DataCache.RemoveCache("GetTabModuleSettings" & TabModuleId.ToString)
        End Sub

#End Region

#End Region

#Region "Obsolete"

        Public Function GetModule(ByVal ModuleId As Integer, ByVal TabId As Integer) As ModuleInfo
            Return GetModule(ModuleId, TabId, True)
        End Function

        <Obsolete("Use the new GetTabModules(ByVal TabId As Integer)")> _
        Public Function GetPortalTabModules(ByVal PortalId As Integer, ByVal TabId As Integer) As ArrayList
            Dim arr As New ArrayList
            For Each kvp As KeyValuePair(Of Integer, ModuleInfo) In GetTabModules(TabId)
                arr.Add(kvp.Value)
            Next
            Return arr
        End Function

        <Obsolete("Use the new UpdateTabModuleOrder(tabid,portalid)")> _
        Public Sub UpdateTabModuleOrder(ByVal TabId As Integer)

            Dim ModuleCounter As Integer
            Dim dr As IDataReader = DataProvider.Instance().GetTabPanes(TabId)
            While dr.Read
                ModuleCounter = 0
                Dim dr2 As IDataReader = DataProvider.Instance().GetTabModuleOrder(TabId, Convert.ToString(dr("PaneName")))
                While dr2.Read
                    ModuleCounter += 1
                    DataProvider.Instance().UpdateModuleOrder(TabId, Convert.ToInt32(dr2("ModuleID")), (ModuleCounter * 2) - 1, Convert.ToString(dr("PaneName")))
                End While
                dr2.Close()
            End While
            dr.Close()

            ' clear module cache
            DataCache.ClearModuleCache(TabId)
        End Sub

#End Region

    End Class


End Namespace
