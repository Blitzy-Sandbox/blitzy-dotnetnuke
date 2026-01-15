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
Imports System.XML
Imports System.io
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.UI.Skins
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Definitions
Imports DotNetNuke.Entities.Profile
Imports DotNetNuke.Security.Membership
Imports DotNetNuke.Services.FileSystem
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.Common.Lists
Imports DotNetNuke.Services.EventQueue

Imports ICSharpCode.SharpZipLib.Zip

Namespace DotNetNuke.Entities.Portals

#Region "Enumerators"

    Public Enum PortalTemplateModuleAction
        Ignore
        Merge
        Replace
    End Enum

#End Region

    Public Class PortalController

#Region "Private Shared Methods"

        Private Shared Function FillPortalInfo(ByVal dr As IDataReader) As PortalInfo
            Return FillPortalInfo(dr, True)
        End Function

        Private Shared Function FillPortalInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As PortalInfo
            Dim objPortalInfo As PortalInfo = Nothing

            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If
            If canContinue Then
                objPortalInfo = New PortalInfo
                objPortalInfo.PortalID = Convert.ToInt32(Null.SetNull(dr("PortalID"), objPortalInfo.PortalID))
                objPortalInfo.PortalName = Convert.ToString(Null.SetNull(dr("PortalName"), objPortalInfo.PortalName))
                objPortalInfo.LogoFile = Convert.ToString(Null.SetNull(dr("LogoFile"), objPortalInfo.LogoFile))
                objPortalInfo.FooterText = Convert.ToString(Null.SetNull(dr("FooterText"), objPortalInfo.FooterText))
                objPortalInfo.ExpiryDate = Convert.ToDateTime(Null.SetNull(dr("ExpiryDate"), objPortalInfo.ExpiryDate))
                objPortalInfo.UserRegistration = Convert.ToInt32(Null.SetNull(dr("UserRegistration"), objPortalInfo.UserRegistration))
                objPortalInfo.BannerAdvertising = Convert.ToInt32(Null.SetNull(dr("BannerAdvertising"), objPortalInfo.BannerAdvertising))
                objPortalInfo.AdministratorId = Convert.ToInt32(Null.SetNull(dr("AdministratorID"), objPortalInfo.AdministratorId))
                objPortalInfo.Email = Convert.ToString(Null.SetNull(dr("Email"), objPortalInfo.Email))
                objPortalInfo.Currency = Convert.ToString(Null.SetNull(dr("Currency"), objPortalInfo.Currency))
                objPortalInfo.HostFee = Convert.ToInt32(Null.SetNull(dr("HostFee"), objPortalInfo.HostFee))
                objPortalInfo.HostSpace = Convert.ToInt32(Null.SetNull(dr("HostSpace"), objPortalInfo.HostSpace))
                objPortalInfo.PageQuota = Convert.ToInt32(Null.SetNull(dr("PageQuota"), objPortalInfo.PageQuota))
                objPortalInfo.UserQuota = Convert.ToInt32(Null.SetNull(dr("UserQuota"), objPortalInfo.UserQuota))
                objPortalInfo.AdministratorRoleId = Convert.ToInt32(Null.SetNull(dr("AdministratorRoleID"), objPortalInfo.AdministratorRoleId))
                objPortalInfo.RegisteredRoleId = Convert.ToInt32(Null.SetNull(dr("RegisteredRoleID"), objPortalInfo.RegisteredRoleId))
                objPortalInfo.Description = Convert.ToString(Null.SetNull(dr("Description"), objPortalInfo.Description))
                objPortalInfo.KeyWords = Convert.ToString(Null.SetNull(dr("KeyWords"), objPortalInfo.KeyWords))
                objPortalInfo.BackgroundFile = Convert.ToString(Null.SetNull(dr("BackGroundFile"), objPortalInfo.BackgroundFile))
                objPortalInfo.GUID = New Guid(Convert.ToString(Null.SetNull(dr("GUID"), objPortalInfo.GUID)))
                objPortalInfo.PaymentProcessor = Convert.ToString(Null.SetNull(dr("PaymentProcessor"), objPortalInfo.PaymentProcessor))
                objPortalInfo.ProcessorUserId = Convert.ToString(Null.SetNull(dr("ProcessorUserId"), objPortalInfo.ProcessorUserId))
                objPortalInfo.ProcessorPassword = Convert.ToString(Null.SetNull(dr("ProcessorPassword"), objPortalInfo.ProcessorPassword))
                objPortalInfo.SiteLogHistory = Convert.ToInt32(Null.SetNull(dr("SiteLogHistory"), objPortalInfo.SiteLogHistory))
                objPortalInfo.SplashTabId = Convert.ToInt32(Null.SetNull(dr("SplashTabID"), objPortalInfo.SplashTabId))
                objPortalInfo.HomeTabId = Convert.ToInt32(Null.SetNull(dr("HomeTabID"), objPortalInfo.HomeTabId))
                objPortalInfo.LoginTabId = Convert.ToInt32(Null.SetNull(dr("LoginTabID"), objPortalInfo.LoginTabId))
                objPortalInfo.UserTabId = Convert.ToInt32(Null.SetNull(dr("UserTabID"), objPortalInfo.UserTabId))
                objPortalInfo.DefaultLanguage = Convert.ToString(Null.SetNull(dr("DefaultLanguage"), objPortalInfo.DefaultLanguage))
                objPortalInfo.TimeZoneOffset = Convert.ToInt32(Null.SetNull(dr("TimeZoneOffset"), objPortalInfo.TimeZoneOffset))
                objPortalInfo.AdminTabId = Convert.ToInt32(Null.SetNull(dr("AdminTabID"), objPortalInfo.AdminTabId))
                objPortalInfo.HomeDirectory = Convert.ToString(Null.SetNull(dr("HomeDirectory"), objPortalInfo.HomeDirectory))
                objPortalInfo.SuperTabId = Convert.ToInt32(Null.SetNull(dr("SuperTabId"), objPortalInfo.SuperTabId))
                objPortalInfo.AdministratorRoleName = Convert.ToString(Null.SetNull(dr("AdministratorRoleName"), objPortalInfo.AdministratorRoleName))
                objPortalInfo.RegisteredRoleName = Convert.ToString(Null.SetNull(dr("RegisteredRoleName"), objPortalInfo.RegisteredRoleName))

                objPortalInfo.Users = Null.NullInteger
                objPortalInfo.Pages = Null.NullInteger
            End If

            Return objPortalInfo
        End Function

        Private Shared Function FillPortalInfoCollection(ByVal dr As IDataReader) As ArrayList
            Dim TotalRecords As Integer
            Return FillPortalInfoCollection(dr, TotalRecords)
        End Function

        Private Shared Function FillPortalInfoCollection(ByVal dr As IDataReader, ByRef totalRecords As Integer) As ArrayList
            Dim arr As New ArrayList
            Try
                Dim obj As PortalInfo
                While dr.Read
                    ' fill business object
                    obj = FillPortalInfo(dr, False)
                    ' add to collection
                    arr.Add(obj)
                End While

                'Get the next result (containing the total)
                Dim nextResult As Boolean = dr.NextResult()

                If nextResult Then
                    'Get the total no of records from the second result
                    totalRecords = GetTotalRecords(dr)
                End If

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

#End Region

#Region "Public Shared Methods"

        Public Shared Sub DeleteExpiredPortals(ByVal serverPath As String)
            For Each portal As PortalInfo In GetExpiredPortals()
                DeletePortal(portal, serverPath)
            Next
        End Sub

        Public Shared Function DeletePortal(ByVal portal As PortalInfo, ByVal serverPath As String) As String
            Dim strPortalName As String
            Dim strMessage As String = String.Empty

            ' check if this is the last portal
            Dim portalCount As Integer = DataProvider.Instance.GetPortalCount()

            If portalCount > 1 Then
                If Not portal Is Nothing Then
                    ' delete custom resource files
                    DeleteFilesRecursive(serverPath, ".Portal-" + portal.PortalID.ToString + ".resx")

                    'If child portal delete child folder
                    Dim objPortalAliasController As New PortalAliasController
                    Dim arr As ArrayList = objPortalAliasController.GetPortalAliasArrayByPortalID(portal.PortalID)
                    If arr.Count > 0 Then
                        Dim objPortalAliasInfo As PortalAliasInfo = CType(arr(0), PortalAliasInfo)
                        strPortalName = GetPortalDomainName(objPortalAliasInfo.HTTPAlias)
                        If Convert.ToBoolean(InStr(1, objPortalAliasInfo.HTTPAlias, "/")) Then
                            strPortalName = Mid(objPortalAliasInfo.HTTPAlias, InStrRev(objPortalAliasInfo.HTTPAlias, "/") + 1)
                        End If
                        If strPortalName <> "" AndAlso System.IO.Directory.Exists(serverPath & strPortalName) Then
                            DeleteFolderRecursive(serverPath & strPortalName)
                        End If
                    End If

                    ' delete upload directory
                    DeleteFolderRecursive(serverPath & "Portals\" & portal.PortalID.ToString)
                    Dim HomeDirectory As String = portal.HomeDirectoryMapPath
                    If System.IO.Directory.Exists(HomeDirectory) Then
                        DeleteFolderRecursive(HomeDirectory)
                    End If

                    ' remove database references
                    Dim objPortalController As New PortalController
                    objPortalController.DeletePortalInfo(portal.PortalID)
                End If
            Else
                strMessage = Localization.GetString("LastPortal")
            End If

            Return strMessage
        End Function

        Public Shared Function GetPortalDictionary() As Dictionary(Of Integer, Integer)

            Dim key As String = DataCache.PortalDictionaryCacheKey

            ' retrieve from cache
            Dim portalDic As Dictionary(Of Integer, Integer) = TryCast(DataCache.GetCache(key), Dictionary(Of Integer, Integer))

            If portalDic Is Nothing Then
                ' create dictionary
                portalDic = New Dictionary(Of Integer, Integer)

                ' portal dictionary caching settings
                Dim timeOut As Int32 = DataCache.PortalDictionaryTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                ' if caching is disabled, do not make this database call as it is too expensive on every request
                If timeOut > 0 Then
                    ' get all tabs
                    Dim intField As Integer
                    Dim dr As IDataReader = DataProvider.Instance().GetAllTabs()
                    Try
                        While dr.Read
                            ' add to dictionary
                            portalDic(Convert.ToInt32(Null.SetNull(dr("TabID"), intField))) = Convert.ToInt32(Null.SetNull(dr("PortalID"), intField))
                        End While
                    Catch exc As Exception
                        LogException(exc)
                    Finally
                        ' close datareader
                        If Not dr Is Nothing Then
                            dr.Close()
                        End If
                    End Try

                    ' cache portal dictionary
                    DataCache.SetCache(key, portalDic, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If

            Return portalDic
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetPortalsByName gets all the portals whose name matches a provided filter expression
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="nameToMatch">The email address to use to find a match.</param>
        ''' <param name="pageIndex">The page of records to return.</param>
        ''' <param name="pageSize">The size of the page</param>
        ''' <param name="totalRecords">The total no of records that satisfy the criteria.</param>
        ''' <returns>An ArrayList of PortalInfo objects.</returns>
        ''' <history>
        '''     [cnurse]	11/17/2006	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Shared Function GetPortalsByName(ByVal nameToMatch As String, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList

            If pageIndex = -1 Then
                pageIndex = 0
                pageSize = Integer.MaxValue
            End If

            Return FillPortalInfoCollection(DataProvider.Instance().GetPortalsByName(nameToMatch, pageIndex, pageSize), totalRecords)

        End Function

        Public Shared Function GetExpiredPortals() As ArrayList
            Return FillPortalInfoCollection(DataProvider.Instance().GetExpiredPortals())
        End Function

#End Region

#Region "Private Methods"

        Private Function CreateProfileDefinitions(ByVal PortalId As Integer, ByVal TemplatePath As String, ByVal TemplateFile As String) As String

            Dim strMessage As String = Null.NullString
            Try
                ' add profile definitions
                Dim xmlDoc As New XmlDocument
                Dim node As XmlNode
                ' open the XML template file
                Try
                    xmlDoc.Load(TemplatePath & TemplateFile)
                Catch
                    ' error
                End Try

                ' parse profile definitions if available
                node = xmlDoc.SelectSingleNode("//portal/profiledefinitions")
                If Not node Is Nothing Then
                    ParseProfileDefinitions(node, PortalId)
                Else ' template does not contain profile definitions ( ie. was created prior to DNN 3.3.0 )
                    ProfileController.AddDefaultDefinitions(PortalId)
                End If

            Catch ex As Exception
                strMessage = Localization.GetString("CreateProfileDefinitions.Error")
            End Try

            Return strMessage

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Creates a new portal based on the portal template provided.
        ''' </summary>
        ''' <param name="PortalName">Name of the portal to be created</param>
        ''' <returns>PortalId of the new portal if there are no errors, -1 otherwise.</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	03/09/2004	Modified to support new template format.
        '''                             Portal template file should be processed before admin.template
        '''     [cnurse]    01/11/2005  Template parsing moved to CreatePortal
        '''     [cnurse]    05/10/2006  Removed unneccessary use of Administrator properties
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function CreatePortal(ByVal PortalName As String, ByVal HomeDirectory As String) As Integer

            ' add portal
            Dim PortalId As Integer = -1
            Try
                ' Use host settings as default values for these parameters
                ' This can be overwritten on the portal template
                Dim datExpiryDate As Date
                If Convert.ToString(Common.Globals.HostSettings("DemoPeriod")) <> "" Then
                    datExpiryDate = Convert.ToDateTime(GetMediumDate(DateAdd(DateInterval.Day, Int32.Parse(Convert.ToString(Common.Globals.HostSettings("DemoPeriod"))), Now()).ToString))
                Else
                    datExpiryDate = Null.NullDate
                End If

                Dim dblHostFee As Double = 0
                If Convert.ToString(Common.Globals.HostSettings("HostFee")) <> "" Then
                    dblHostFee = Convert.ToDouble(Common.Globals.HostSettings("HostFee"))
                End If

                Dim dblHostSpace As Double = 0
                If Convert.ToString(Common.Globals.HostSettings("HostSpace")) <> "" Then
                    dblHostSpace = Convert.ToDouble(Common.Globals.HostSettings("HostSpace"))
                End If

                Dim intPageQuota As Integer = 0
                If Convert.ToString(Common.Globals.HostSettings("PageQuota")) <> "" Then
                    intPageQuota = Convert.ToInt32(Common.Globals.HostSettings("PageQuota"))
                End If

                Dim intUserQuota As Integer = 0
                If Convert.ToString(Common.Globals.HostSettings("UserQuota")) <> "" Then
                    intUserQuota = Convert.ToInt32(Common.Globals.HostSettings("UserQuota"))
                End If

                Dim intSiteLogHistory As Integer = -1
                If Convert.ToString(Common.Globals.HostSettings("SiteLogHistory")) <> "" Then
                    intSiteLogHistory = Convert.ToInt32(Common.Globals.HostSettings("SiteLogHistory"))
                End If

                Dim strCurrency As String = Convert.ToString(Common.Globals.HostSettings("HostCurrency"))
                If strCurrency = "" Then
                    strCurrency = "USD"
                End If
                PortalId = DataProvider.Instance().CreatePortal(PortalName, strCurrency, datExpiryDate, dblHostFee, dblHostSpace, intPageQuota, intUserQuota, intSiteLogHistory, HomeDirectory)

            Catch
                ' error creating portal
            End Try

            Return PortalId

        End Function

        Private Function CreateRole(ByVal PortalId As Integer, ByVal roleName As String, ByVal description As String, ByVal serviceFee As Single, ByVal billingPeriod As Integer, ByVal billingFrequency As String, ByVal trialFee As Single, ByVal trialPeriod As Integer, ByVal trialFrequency As String, ByVal isPublic As Boolean, ByVal isAuto As Boolean) As Integer

            Dim objRoleInfo As New RoleInfo
            Dim objRoleController As New RoleController
            Dim RoleId As Integer

            'First check if the role exists
            objRoleInfo = objRoleController.GetRoleByName(PortalId, roleName)

            If objRoleInfo Is Nothing Then

                objRoleInfo = New RoleInfo
                objRoleInfo.PortalID = PortalId
                objRoleInfo.RoleName = roleName
                objRoleInfo.RoleGroupID = Null.NullInteger
                objRoleInfo.Description = description
                objRoleInfo.ServiceFee = CType(IIf(serviceFee < 0, 0, serviceFee), Single)
                objRoleInfo.BillingPeriod = billingPeriod
                objRoleInfo.BillingFrequency = billingFrequency
                objRoleInfo.TrialFee = CType(IIf(trialFee < 0, 0, trialFee), Single)
                objRoleInfo.TrialPeriod = trialPeriod
                objRoleInfo.TrialFrequency = trialFrequency
                objRoleInfo.IsPublic = isPublic
                objRoleInfo.AutoAssignment = isAuto
                RoleId = objRoleController.AddRole(objRoleInfo)
            Else
                RoleId = objRoleInfo.RoleID
            End If

            Return RoleId

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes all Files from the template
        ''' </summary>
        ''' <param name="nodeFiles">Template file node for the Files</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <history>
        ''' 	[cnurse]	11/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParseFiles(ByVal nodeFiles As XmlNodeList, ByVal PortalId As Integer, ByVal objFolder As FolderInfo)

            Dim node As XmlNode
            Dim FileId As Integer
            Dim objController As New FileController
            Dim objInfo As DotNetNuke.Services.FileSystem.FileInfo
            Dim fileName As String

            For Each node In nodeFiles
                fileName = XmlUtils.GetNodeValue(node, "filename")

                'First check if the file exists
                objInfo = objController.GetFile(fileName, PortalId, objFolder.FolderID)

                If objInfo Is Nothing Then
                    objInfo = New DotNetNuke.Services.FileSystem.FileInfo
                    objInfo.PortalId = PortalId
                    objInfo.FileName = fileName
                    objInfo.Extension = XmlUtils.GetNodeValue(node, "extension")
                    objInfo.Size = XmlUtils.GetNodeValueInt(node, "size")
                    objInfo.Width = XmlUtils.GetNodeValueInt(node, "width")
                    objInfo.Height = XmlUtils.GetNodeValueInt(node, "height")
                    objInfo.ContentType = XmlUtils.GetNodeValue(node, "contenttype")
                    objInfo.FolderId = objFolder.FolderID
                    objInfo.Folder = objFolder.FolderPath

                    'Save new File 
                    FileId = objController.AddFile(objInfo)
                Else
                    'Get Id from File
                    FileId = objInfo.FileId
                End If
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes all Folders from the template
        ''' </summary>
        ''' <param name="nodeFolders">Template file node for the Folders</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <history>
        ''' 	[cnurse]	11/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParseFolders(ByVal nodeFolders As XmlNode, ByVal PortalId As Integer)

            Dim node As XmlNode
            Dim FolderId As Integer
            Dim objController As New FolderController
            Dim objInfo As FolderInfo
            Dim folderPath As String
            Dim storageLocation As Integer
            Dim isProtected As Boolean = False

            For Each node In nodeFolders.SelectNodes("//folder")
                folderPath = XmlUtils.GetNodeValue(node, "folderpath")

                'First check if the folder exists
                objInfo = objController.GetFolder(PortalId, folderPath, False)

                If objInfo Is Nothing Then
                    isProtected = FileSystemUtils.DefaultProtectedFolders(folderPath)
                    If isProtected = True Then
                        ' protected folders must use insecure storage
                        storageLocation = FolderController.StorageLocationTypes.InsecureFileSystem
                    Else
                        storageLocation = CType(XmlUtils.GetNodeValue(node, "storagelocation", "0"), Integer)
                        isProtected = CType(XmlUtils.GetNodeValue(node, "isprotected", "0"), Boolean)
                    End If
                    'Save new folder 
                    FolderId = objController.AddFolder(PortalId, folderPath, storageLocation, isProtected, False)
                    objInfo = objController.GetFolder(PortalId, folderPath, False)
                Else
                    'Get Id from Folder
                    FolderId = objInfo.FolderID
                End If

                Dim nodeFolderPermissions As XmlNodeList = node.SelectNodes("folderpermissions/permission")
                ParseFolderPermissions(nodeFolderPermissions, PortalId, FolderId, folderPath)

                Dim nodeFiles As XmlNodeList = node.SelectNodes("files/file")
                If folderPath <> "" Then
                    folderPath += "/"
                End If
                ParseFiles(nodeFiles, PortalId, objInfo)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Parses folder permissions
        ''' </summary>
        ''' <param name="nodeFolderPermissions">Node for folder permissions</param>
        ''' <param name="PortalID">PortalId of new portal</param>
        ''' <param name="FolderId">FolderId of folder being processed</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParseFolderPermissions(ByVal nodeFolderPermissions As XmlNodeList, ByVal PortalID As Integer, ByVal FolderId As Integer, ByVal folderPath As String)
            Dim objFolderPermissions As New Security.Permissions.FolderPermissionCollection
            Dim objPermissionController As New Security.Permissions.PermissionController
            Dim objPermission As Security.Permissions.PermissionInfo
            Dim objFolderPermissionController As New Security.Permissions.FolderPermissionController
            Dim objRoleController As New RoleController
            Dim objRole As RoleInfo
            Dim RoleID As Integer
            Dim PermissionID As Integer
            Dim PermissionKey, PermissionCode As String
            Dim RoleName As String
            Dim AllowAccess As Boolean
            Dim arrPermissions As ArrayList
            Dim i As Integer
            Dim xmlFolderPermission As XmlNode

            For Each xmlFolderPermission In nodeFolderPermissions
                PermissionKey = XmlUtils.GetNodeValue(xmlFolderPermission, "permissionkey")
                PermissionCode = XmlUtils.GetNodeValue(xmlFolderPermission, "permissioncode")
                RoleName = XmlUtils.GetNodeValue(xmlFolderPermission, "rolename")
                AllowAccess = XmlUtils.GetNodeValueBoolean(xmlFolderPermission, "allowaccess")
                arrPermissions = objPermissionController.GetPermissionByCodeAndKey(PermissionCode, PermissionKey)

                For i = 0 To arrPermissions.Count - 1
                    objPermission = CType(arrPermissions(i), Security.Permissions.PermissionInfo)
                    PermissionID = objPermission.PermissionID
                Next
                RoleID = Integer.MinValue
                Select Case RoleName
                    Case glbRoleAllUsersName
                        RoleID = Convert.ToInt32(glbRoleAllUsers)
                    Case Common.Globals.glbRoleUnauthUserName
                        RoleID = Convert.ToInt32(glbRoleUnauthUser)
                    Case Else
                        objRole = objRoleController.GetRoleByName(PortalID, RoleName)
                        If Not objRole Is Nothing Then
                            RoleID = objRole.RoleID
                        End If
                End Select

                ' if role was found add, otherwise ignore
                If RoleID <> Integer.MinValue Then
                    If AllowAccess Then
                        FileSystemUtils.SetFolderPermission(PortalID, FolderId, PermissionID, RoleID, folderPath)
                    End If
                End If
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes the settings node
        ''' </summary>
        ''' <param name="nodeSettings">Template file node for the settings</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	27/08/2004	Created
        ''' 	[VMasanas]	15/10/2004	Modified for new skin structure
        '''     [cnurse]    11/21/2004  Modified to use GetNodeValueDate for ExpiryDate
        '''     [VMasanas]  02/21/2005  Modified to not overwrite ExpiryDate if not present
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParsePortalSettings(ByVal nodeSettings As XmlNode, ByVal PortalId As Integer)

            Dim objPortal As PortalInfo
            objPortal = GetPortal(PortalId)

            objPortal.LogoFile = ImportFile(PortalId, XmlUtils.GetNodeValue(nodeSettings, "logofile"))
            objPortal.FooterText = XmlUtils.GetNodeValue(nodeSettings, "footertext")
            If Not nodeSettings.SelectSingleNode("expirydate") Is Nothing Then
                objPortal.ExpiryDate = XmlUtils.GetNodeValueDate(nodeSettings, "expirydate", Null.NullDate)
            End If
            objPortal.UserRegistration = XmlUtils.GetNodeValueInt(nodeSettings, "userregistration")
            objPortal.BannerAdvertising = XmlUtils.GetNodeValueInt(nodeSettings, "banneradvertising")
            If XmlUtils.GetNodeValue(nodeSettings, "currency") <> "" Then
                objPortal.Currency = XmlUtils.GetNodeValue(nodeSettings, "currency")
            End If
            If XmlUtils.GetNodeValue(nodeSettings, "hostfee") <> "" Then
                objPortal.HostFee = XmlUtils.GetNodeValueSingle(nodeSettings, "hostfee")
            End If
            If XmlUtils.GetNodeValue(nodeSettings, "hostspace") <> "" Then
                objPortal.HostSpace = XmlUtils.GetNodeValueInt(nodeSettings, "hostspace")
            End If
            If XmlUtils.GetNodeValue(nodeSettings, "pagequota") <> "" Then
                objPortal.PageQuota = XmlUtils.GetNodeValueInt(nodeSettings, "pagequota")
            End If
            If XmlUtils.GetNodeValue(nodeSettings, "userquota") <> "" Then
                objPortal.UserQuota = XmlUtils.GetNodeValueInt(nodeSettings, "userquota")
            End If
            objPortal.BackgroundFile = XmlUtils.GetNodeValue(nodeSettings, "backgroundfile")
            objPortal.PaymentProcessor = XmlUtils.GetNodeValue(nodeSettings, "paymentprocessor")
            If XmlUtils.GetNodeValue(nodeSettings, "siteloghistory") <> "" Then
                objPortal.SiteLogHistory = XmlUtils.GetNodeValueInt(nodeSettings, "siteloghistory")
            End If
            objPortal.DefaultLanguage = XmlUtils.GetNodeValue(nodeSettings, "defaultlanguage", "en-US")
            objPortal.TimeZoneOffset = XmlUtils.GetNodeValueInt(nodeSettings, "timezoneoffset", -8)

            UpdatePortalInfo(objPortal.PortalID, objPortal.PortalName, objPortal.LogoFile, objPortal.FooterText, _
             objPortal.ExpiryDate, objPortal.UserRegistration, objPortal.BannerAdvertising, objPortal.Currency, objPortal.AdministratorId, objPortal.HostFee, _
             objPortal.HostSpace, objPortal.PageQuota, objPortal.UserQuota, objPortal.PaymentProcessor, objPortal.ProcessorUserId, objPortal.ProcessorPassword, objPortal.Description, _
             objPortal.KeyWords, objPortal.BackgroundFile, objPortal.SiteLogHistory, objPortal.SplashTabId, objPortal.HomeTabId, objPortal.LoginTabId, objPortal.UserTabId, _
             objPortal.DefaultLanguage, objPortal.TimeZoneOffset, objPortal.HomeDirectory)

            ' set portal skins and containers
            If XmlUtils.GetNodeValue(nodeSettings, "skinsrc", "") <> "" Then
                SkinController.SetSkin(SkinInfo.RootSkin, PortalId, SkinType.Portal, XmlUtils.GetNodeValue(nodeSettings, "skinsrc", ""))
            End If
            If XmlUtils.GetNodeValue(nodeSettings, "skinsrcadmin", "") <> "" Then
                SkinController.SetSkin(SkinInfo.RootSkin, PortalId, SkinType.Admin, XmlUtils.GetNodeValue(nodeSettings, "skinsrcadmin", ""))
            End If
            If XmlUtils.GetNodeValue(nodeSettings, "containersrc", "") <> "" Then
                SkinController.SetSkin(SkinInfo.RootContainer, PortalId, SkinType.Portal, XmlUtils.GetNodeValue(nodeSettings, "containersrc", ""))
            End If
            If XmlUtils.GetNodeValue(nodeSettings, "containersrcadmin", "") <> "" Then
                SkinController.SetSkin(SkinInfo.RootContainer, PortalId, SkinType.Admin, XmlUtils.GetNodeValue(nodeSettings, "containersrcadmin", ""))
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes all Profile Definitions from the template
        ''' </summary>
        ''' <param name="nodeProfileDefinitions">Template file node for the Profile Definitions</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParseProfileDefinitions(ByVal nodeProfileDefinitions As XmlNode, ByVal PortalId As Integer)

            Dim node As XmlNode

            Dim objListController As New ListController
            Dim colDataTypes As ListEntryInfoCollection = objListController.GetListEntryInfoCollection("DataType")

            Dim OrderCounter As Integer = -1

            Dim objProfileDefinition As ProfilePropertyDefinition

            For Each node In nodeProfileDefinitions.SelectNodes("//profiledefinition")
                OrderCounter += 2

                Dim typeInfo As ListEntryInfo = colDataTypes.Item("DataType:" + XmlUtils.GetNodeValue(node, "datatype"))
                If typeInfo Is Nothing Then
                    typeInfo = colDataTypes.Item("DataType:Unknown")
                End If

                objProfileDefinition = New ProfilePropertyDefinition(PortalId)
                objProfileDefinition.DataType = typeInfo.EntryID
                objProfileDefinition.DefaultValue = ""
                objProfileDefinition.ModuleDefId = Null.NullInteger
                objProfileDefinition.PropertyCategory = XmlUtils.GetNodeValue(node, "propertycategory")
                objProfileDefinition.PropertyName = XmlUtils.GetNodeValue(node, "propertyname")
                objProfileDefinition.Required = False
                objProfileDefinition.Visible = True
                objProfileDefinition.ViewOrder = OrderCounter
                objProfileDefinition.Length = XmlUtils.GetNodeValueInt(node, "length")

                ProfileController.AddPropertyDefinition(objProfileDefinition)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes a node for a Role and creates a new Role based on the information gathered from the node
        ''' </summary>
        ''' <param name="nodeRole">Template file node for the role</param>
        ''' <param name="portalid">PortalId of the new portal</param>
        ''' <returns>RoleId of the created role</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	26/08/2004	Created
        '''		[cnurse]	15/10/2004	Modified to allow for merging template
        '''								with existing pages
        '''     [cnurse]    12/07/2004  modified to use new CreateRole method, which takes care of
        '''                             checking if role exists
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function ParseRole(ByVal nodeRole As XmlNode, ByVal PortalId As Integer) As Integer

            Dim RoleName As String = XmlUtils.GetNodeValue(nodeRole, "rolename")
            Dim Description As String = XmlUtils.GetNodeValue(nodeRole, "description")
            Dim ServiceFee As Single = XmlUtils.GetNodeValueSingle(nodeRole, "servicefee")
            Dim BillingPeriod As Integer = XmlUtils.GetNodeValueInt(nodeRole, "billingperiod")
            Dim BillingFrequency As String = XmlUtils.GetNodeValue(nodeRole, "billingfrequency", "M")
            Dim TrialFee As Single = XmlUtils.GetNodeValueSingle(nodeRole, "trialfee")
            Dim TrialPeriod As Integer = XmlUtils.GetNodeValueInt(nodeRole, "trialperiod")
            Dim TrialFrequency As String = XmlUtils.GetNodeValue(nodeRole, "trialfrequency", "N")
            Dim IsPublic As Boolean = XmlUtils.GetNodeValueBoolean(nodeRole, "ispublic")
            Dim AutoAssignment As Boolean = XmlUtils.GetNodeValueBoolean(nodeRole, "autoassignment")

            'Call Create Role
            Return CreateRole(PortalId, RoleName, Description, ServiceFee, BillingPeriod, BillingFrequency, TrialFee, TrialPeriod, TrialFrequency, IsPublic, AutoAssignment)

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes all Roles from the template
        ''' </summary>
        ''' <param name="nodeRoles">Template file node for the Roles</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <param name="AdministratorId">New portal's Administrator</param>
        ''' <param name="AdministratorRole">Used to return to caller the id of the Administrators Role if found</param>
        ''' <param name="RegisteredRole">Used to return to caller the id of the Registered Users Role if found</param>
        ''' <param name="SubscriberRole">Used to return to caller the id of the Subscribers Role if found</param>
        ''' <remarks>
        ''' There must be one role for the Administrators function. Only the first node with this mark will be used as the Administrators Role.
        ''' There must be one role for the Registered Users function. Only the first node with this mark will be used as the Registered Users Role.
        ''' There must be one role for the Subscribers function. Only the first node with this mark will be used as the Subscribers Role.
        ''' If these two minimum roles are not found on the template they will be created with default values.
        ''' 
        ''' The administrator user will be added to the following roles: Administrators, Registered Users and any role specified with autoassignment=true
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	26/08/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParseRoles(ByVal nodeRoles As XmlNode, ByVal PortalId As Integer, ByVal AdministratorId As Integer, ByRef AdministratorRole As Integer, ByRef RegisteredRole As Integer, ByRef SubscriberRole As Integer)

            Dim node As XmlNode
            Dim foundAdminRole As Boolean = False
            Dim foundRegisteredRole As Boolean = False
            Dim foundSubscriberRole As Boolean = False
            Dim RoleId As Integer

            For Each node In nodeRoles.SelectNodes("//role")
                RoleId = ParseRole(node, PortalId)

                ' check if this is the admin role (only first found is selected as admin)
                If Not foundAdminRole AndAlso XmlUtils.GetNodeValue(node, "roletype", "") = "adminrole" Then
                    foundAdminRole = True
                    AdministratorRole = RoleId
                End If

                ' check if this is the registered role (only first found is selected as registered)
                If Not foundRegisteredRole AndAlso XmlUtils.GetNodeValue(node, "roletype", "") = "registeredrole" Then
                    foundRegisteredRole = True
                    RegisteredRole = RoleId
                End If

                ' check if this is the subscriber role (only first found is selected as subscriber)
                If Not foundSubscriberRole AndAlso XmlUtils.GetNodeValue(node, "roletype", "") = "subscriberrole" Then
                    foundSubscriberRole = True
                    SubscriberRole = RoleId
                End If
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes all tabs from the template
        ''' </summary>
        ''' <param name="nodeTabs">Template file node for the tabs</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <param name="IsAdminTemplate">True when processing admin template, false when processing portal template</param>
        ''' <param name="mergeTabs">Flag to determine whether Module content is merged.</param>
        ''' <param name="IsNewPortal">Flag to determine is the template is applied to an existing portal or a new one.</param>
        ''' <remarks>
        ''' When a special tab is found (HomeTab, UserTab, LoginTab, AdminTab) portal information will be updated.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	26/08/2004	Removed code to allow multiple tabs with same name.
        ''' 	[VMasanas]	15/10/2004	Modified for new skin structure
        '''		[cnurse]	15/10/2004	Modified to allow for merging template
        '''								with existing pages
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ParseTabs(ByVal nodeTabs As XmlNode, ByVal PortalId As Integer, ByVal IsAdminTemplate As Boolean, ByVal mergeTabs As PortalTemplateModuleAction, ByVal IsNewPortal As Boolean)

            Dim nodeTab As XmlNode
            'used to control if modules are true modules or instances
            'will hold module ID from template / new module ID so new instances can reference right moduleid
            'only first one from the template will be create as a true module, 
            'others will be moduleinstances (tabmodules)
            Dim hModules As New Hashtable
            Dim hTabs As New Hashtable

            'if running from wizard we need to pre populate htabs with existing tabs so ParseTab 
            'can find all existing ones
            Dim tabname As String
            If Not IsNewPortal Then
                Dim hTabNames As New Hashtable
                Dim objTabs As New TabController
                For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(PortalId)
                    Dim objTab As TabInfo = tabPair.Value

                    If Not objTab.IsDeleted And Not objTab.IsAdminTab Then
                        tabname = objTab.TabName
                        If Not Null.IsNull(objTab.ParentId) Then
                            tabname = CType(hTabNames(objTab.ParentId), String) + "/" + objTab.TabName
                        End If
                        hTabNames.Add(objTab.TabID, tabname)
                    End If
                Next

                'when parsing tabs we will need tabid given tabname
                For Each i As Integer In hTabNames.Keys
                    If hTabs(hTabNames(i)) Is Nothing Then
                        hTabs.Add(hTabNames(i), i)
                    End If
                Next
                hTabNames = Nothing
            End If

            For Each nodeTab In nodeTabs.SelectNodes("//tab")
                ParseTab(nodeTab, PortalId, IsAdminTemplate, mergeTabs, hModules, hTabs, IsNewPortal)
            Next
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes a single tab from the template
        ''' </summary>
        ''' <param name="nodeTab">Template file node for the tabs</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <param name="IsAdminTemplate">True when processing admin template, false when processing portal template</param>
        ''' <param name="mergeTabs">Flag to determine whether Module content is merged.</param>
        ''' <param name="hModules">Used to control if modules are true modules or instances</param>
        ''' <param name="hTabs">Supporting object to build the tab hierarchy</param>
        ''' <param name="IsNewPortal">Flag to determine is the template is applied to an existing portal or a new one.</param>
        ''' <remarks>
        ''' When a special tab is found (HomeTab, UserTab, LoginTab, AdminTab) portal information will be updated.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	26/08/2004	Removed code to allow multiple tabs with same name.
        ''' 	[VMasanas]	15/10/2004	Modified for new skin structure
        '''		[cnurse]	15/10/2004	Modified to allow for merging template
        '''								with existing pages
        '''     [cnurse]    11/21/2204  modified to use GetNodeValueDate for Start and End Dates
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function ParseTab(ByVal nodeTab As XmlNode, ByVal PortalId As Integer, ByVal IsAdminTemplate As Boolean, ByVal mergeTabs As PortalTemplateModuleAction, ByRef hModules As Hashtable, ByRef hTabs As Hashtable, ByVal IsNewPortal As Boolean) As Integer
            Dim objTab As TabInfo = Nothing
            Dim objTabs As New TabController
            Dim strName As String = XmlUtils.GetNodeValue(nodeTab, "name")
            Dim objportal As PortalInfo = GetPortal(PortalId)

            If strName <> "" Then
                If Not IsNewPortal Then ' running from wizard: try to find the tab by path
                    Dim parenttabname As String = ""

                    If XmlUtils.GetNodeValue(nodeTab, "parent") <> "" Then
                        parenttabname = XmlUtils.GetNodeValue(nodeTab, "parent") + "/"
                    End If
                    If Not hTabs(parenttabname + strName) Is Nothing Then
                        objTab = objTabs.GetTab(Convert.ToInt32(hTabs(parenttabname + strName)), PortalId, False)
                    End If
                End If

                If objTab Is Nothing Or IsNewPortal Then
                    objTab = TabController.DeserializeTab(nodeTab, Nothing, hTabs, PortalId, IsAdminTemplate, mergeTabs, hModules)
                End If

                If IsAdminTemplate Then
                    ' when processing the admin template we should identify the Admin tab
                    If objTab.TabName = "Admin" Then
                        objportal.AdminTabId = objTab.TabID
                        DataProvider.Instance().UpdatePortalSetup(PortalId, objportal.AdministratorId, objportal.AdministratorRoleId, objportal.RegisteredRoleId, objportal.SplashTabId, objportal.HomeTabId, objportal.LoginTabId, objportal.UserTabId, objportal.AdminTabId)
                    End If
                Else
                    ' when processing the portal template we can find: hometab, usertab, logintab
                    Select Case XmlUtils.GetNodeValue(nodeTab, "tabtype", "")
                        Case "splashtab"
                            objportal.SplashTabId = objTab.TabID
                            DataProvider.Instance().UpdatePortalSetup(PortalId, objportal.AdministratorId, objportal.AdministratorRoleId, objportal.RegisteredRoleId, objportal.SplashTabId, objportal.HomeTabId, objportal.LoginTabId, objportal.UserTabId, objportal.AdminTabId)
                        Case "hometab"
                            objportal.HomeTabId = objTab.TabID
                            DataProvider.Instance().UpdatePortalSetup(PortalId, objportal.AdministratorId, objportal.AdministratorRoleId, objportal.RegisteredRoleId, objportal.SplashTabId, objportal.HomeTabId, objportal.LoginTabId, objportal.UserTabId, objportal.AdminTabId)
                        Case "logintab"
                            objportal.LoginTabId = objTab.TabID
                            DataProvider.Instance().UpdatePortalSetup(PortalId, objportal.AdministratorId, objportal.AdministratorRoleId, objportal.RegisteredRoleId, objportal.SplashTabId, objportal.HomeTabId, objportal.LoginTabId, objportal.UserTabId, objportal.AdminTabId)
                        Case "usertab"
                            objportal.UserTabId = objTab.TabID
                            DataProvider.Instance().UpdatePortalSetup(PortalId, objportal.AdministratorId, objportal.AdministratorRoleId, objportal.RegisteredRoleId, objportal.SplashTabId, objportal.HomeTabId, objportal.LoginTabId, objportal.UserTabId, objportal.AdminTabId)
                    End Select
                End If
            End If
        End Function

        Private Sub UpdatePortalSetup(ByVal PortalId As Integer, ByVal AdministratorId As Integer, ByVal AdministratorRoleId As Integer, ByVal RegisteredRoleId As Integer, ByVal SplashTabId As Integer, ByVal HomeTabId As Integer, ByVal LoginTabId As Integer, ByVal UserTabId As Integer, ByVal AdminTabId As Integer)
            DataProvider.Instance().UpdatePortalSetup(PortalId, AdministratorId, AdministratorRoleId, RegisteredRoleId, SplashTabId, HomeTabId, LoginTabId, UserTabId, AdminTabId)
            DataCache.ClearPortalCache(PortalId, True)
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Creates a new portal alias
        ''' </summary>
        ''' <param name="PortalId">Id of the portal</param>
        ''' <param name="PortalAlias">Portal Alias to be created</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        '''     [cnurse]    01/11/2005  created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub AddPortalAlias(ByVal PortalId As Integer, ByVal PortalAlias As String)

            Dim objPortalAliasController As New PortalAliasController

            'Check if the Alias exists
            Dim objPortalAliasInfo As PortalAliasInfo = objPortalAliasController.GetPortalAlias(PortalAlias, PortalId)

            'If alias does not exist add new
            If objPortalAliasInfo Is Nothing Then
                objPortalAliasInfo = New PortalAliasInfo
                objPortalAliasInfo.PortalID = PortalId
                objPortalAliasInfo.HTTPAlias = PortalAlias
                objPortalAliasController.AddPortalAlias(objPortalAliasInfo)
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Creates a new portal.
        ''' </summary>
        ''' <param name="PortalName">Name of the portal to be created</param>
        ''' <param name="FirstName">Portal Administrator's first name</param>
        ''' <param name="LastName">Portal Administrator's last name</param>
        ''' <param name="Username">Portal Administrator's username</param>
        ''' <param name="Password">Portal Administrator's password</param>
        ''' <param name="Email">Portal Administrator's email</param>
        ''' <param name="Description">Description for the new portal</param>
        ''' <param name="KeyWords">KeyWords for the new portal</param>
        ''' <param name="TemplatePath">Path where the templates are stored</param>
        ''' <param name="TemplateFile">Template file</param>
        ''' <param name="PortalAlias">Portal Alias String</param>
        ''' <param name="ServerPath">The Path to the root of the Application</param>
        ''' <param name="ChildPath">The Path to the Child Portal Folder</param>
        ''' <param name="IsChildPortal">True if this is a child portal</param>
        ''' <returns>PortalId of the new portal if there are no errors, -1 otherwise.</returns>
        ''' <remarks>
        ''' After the selected portal template is parsed the admin template ("admin.template") will be
        ''' also processed. The admin template should only contain the "Admin" menu since it's the same
        ''' on all portals. The selected portal template can contain a <settings/> node to specify portal
        ''' properties and a <roles/> node to define the roles that will be created on the portal by default.
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	created (most of this code was moved from SignUp.ascx.vb)
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function CreatePortal(ByVal PortalName As String, ByVal FirstName As String, ByVal LastName As String, ByVal Username As String, ByVal Password As String, ByVal Email As String, ByVal Description As String, ByVal KeyWords As String, ByVal TemplatePath As String, ByVal TemplateFile As String, ByVal HomeDirectory As String, ByVal PortalAlias As String, ByVal ServerPath As String, ByVal ChildPath As String, ByVal IsChildPortal As Boolean) As Integer

            Dim objFolderController As New Services.FileSystem.FolderController
            Dim strMessage As String = Null.NullString
            Dim AdministratorId As Integer = Null.NullInteger
            Dim objAdminUser As New UserInfo

            'Attempt to create a new portal
            Dim intPortalId As Integer = CreatePortal(PortalName, HomeDirectory)

            If intPortalId <> -1 Then
                If HomeDirectory = "" Then
                    HomeDirectory = "Portals/" + intPortalId.ToString
                End If
                Dim MappedHomeDirectory As String = objFolderController.GetMappedDirectory(Common.Globals.ApplicationPath + "/" + HomeDirectory + "/")

                strMessage += CreateProfileDefinitions(intPortalId, TemplatePath, TemplateFile)
                If strMessage = Null.NullString Then
                    ' add administrator
                    Try
                        objAdminUser.PortalID = intPortalId
                        objAdminUser.FirstName = FirstName
                        objAdminUser.LastName = LastName
                        objAdminUser.Username = Username
                        objAdminUser.DisplayName = FirstName + " " + LastName
                        objAdminUser.Membership.Password = Password
                        objAdminUser.Email = Email
                        objAdminUser.IsSuperUser = False
                        objAdminUser.Membership.Approved = True

                        objAdminUser.Profile.FirstName = FirstName
                        objAdminUser.Profile.LastName = LastName

                        Dim createStatus As UserCreateStatus = UserController.CreateUser(objAdminUser)

                        If createStatus = UserCreateStatus.Success Then
                            AdministratorId = objAdminUser.UserID
                        Else
                            strMessage += UserController.GetUserCreateStatus(createStatus)
                        End If
                    Catch Exc As Exception
                        strMessage += Localization.GetString("CreateAdminUser.Error") + Exc.Message + Exc.StackTrace
                    End Try
                Else
                    Throw New Exception(strMessage)
                End If

                If strMessage = "" And AdministratorId > 0 Then
                    Try
                        ' the upload directory may already exist if this is a new DB working with a previously installed application
                        If Directory.Exists(MappedHomeDirectory) Then
                            DeleteFolderRecursive(MappedHomeDirectory)
                        End If
                    Catch Exc As Exception
                        strMessage += Localization.GetString("DeleteUploadFolder.Error") + Exc.Message + Exc.StackTrace
                    End Try

                    'Set up Child Portal
                    If strMessage = Null.NullString Then
                        Try
                            If IsChildPortal Then
                                ' create the subdirectory for the new portal
                                If Not Directory.Exists(ChildPath) Then
                                    System.IO.Directory.CreateDirectory(ChildPath)
                                End If

                                ' create the subhost default.aspx file
                                If Not File.Exists(ChildPath & "\" & glbDefaultPage) Then
                                    System.IO.File.Copy(Common.Globals.HostMapPath & "subhost.aspx", ChildPath & "\" & glbDefaultPage)
                                End If
                            End If
                        Catch Exc As Exception
                            strMessage += Localization.GetString("ChildPortal.Error") + Exc.Message + Exc.StackTrace
                        End Try
                    Else
                        Throw New Exception(strMessage)
                    End If

                    If strMessage = Null.NullString Then
                        Try
                            ' create the upload directory for the new portal
                            System.IO.Directory.CreateDirectory(MappedHomeDirectory)

                            ' process zip resource file if present
                            ProcessResourceFile(MappedHomeDirectory, TemplatePath & TemplateFile)
                        Catch Exc As Exception
                            strMessage += Localization.GetString("ChildPortal.Error") + Exc.Message + Exc.StackTrace
                        End Try
                    Else
                        Throw New Exception(strMessage)
                    End If

                    If strMessage = Null.NullString Then
                        ' parse portal template
                        Try
                            ParseTemplate(intPortalId, TemplatePath, TemplateFile, AdministratorId, PortalTemplateModuleAction.Replace, True)
                        Catch Exc As Exception
                            strMessage += Localization.GetString("PortalTemplate.Error") + Exc.Message + Exc.StackTrace
                        End Try

                        ' parse admin template
                        Try
                            ParseTemplate(intPortalId, TemplatePath, "admin.template", AdministratorId, PortalTemplateModuleAction.Replace, True)
                        Catch Exc As Exception
                            strMessage += Localization.GetString("AdminTemplate.Error") + Exc.Message + Exc.StackTrace
                        End Try

                        'copy the default page template
                        Dim strHostTemplateFile As String = HostMapPath & "Templates\Default.page.template"
                        If File.Exists(strHostTemplateFile) Then
                            Dim strPortalTemplateFolder As String = MappedHomeDirectory & "Templates\"
                            If Not Directory.Exists(strPortalTemplateFolder) Then
                                'Create Portal Templates folder
                                Directory.CreateDirectory(strPortalTemplateFolder)
                            End If
                            Dim strPortalTemplateFile As String = strPortalTemplateFolder + "Default.page.template"
                            If Not File.Exists(strPortalTemplateFile) Then
                                File.Copy(strHostTemplateFile, strPortalTemplateFile)

                                'Synchronize the Templates folder to ensure the templates are accessible
                                FileSystemUtils.SynchronizeFolder(intPortalId, strPortalTemplateFolder, "Templates/", False, True, True)
                            End If
                        End If
                    Else
                        Throw New Exception(strMessage)
                    End If

                    If strMessage = Null.NullString Then
                        ' update portal setup
                        Dim objportal As PortalInfo = GetPortal(intPortalId)

                        ' update portal info
                        objportal.Description = Description
                        objportal.KeyWords = KeyWords
                        UpdatePortalInfo(objportal.PortalID, objportal.PortalName, objportal.LogoFile, objportal.FooterText, _
                         objportal.ExpiryDate, objportal.UserRegistration, objportal.BannerAdvertising, objportal.Currency, objportal.AdministratorId, objportal.HostFee, _
                         objportal.HostSpace, objportal.PageQuota, objportal.UserQuota, objportal.PaymentProcessor, objportal.ProcessorUserId, objportal.ProcessorPassword, objportal.Description, _
                         objportal.KeyWords, objportal.BackgroundFile, objportal.SiteLogHistory, objportal.SplashTabId, objportal.HomeTabId, objportal.LoginTabId, objportal.UserTabId, _
                         objportal.DefaultLanguage, objportal.TimeZoneOffset, objportal.HomeDirectory)

                        'Update Administrators Locale/TimeZone
                        objAdminUser.Profile.PreferredLocale = objportal.DefaultLanguage
                        objAdminUser.Profile.TimeZone = objportal.TimeZoneOffset

                        'Save Admin User
                        UserController.UpdateUser(objportal.PortalID, objAdminUser)

                        'clear portal alias cache
                        DataCache.ClearHostCache(True)

                        ' clear roles cache
                        DataCache.RemoveCache("GetRoles")

                        'Create Portal Alias
                        AddPortalAlias(intPortalId, PortalAlias)

                        ' log installation event
                        Try
                            Dim objEventLogInfo As New Services.Log.EventLog.LogInfo
                            objEventLogInfo.BypassBuffering = True
                            objEventLogInfo.LogTypeKey = Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString
                            objEventLogInfo.LogTypeKey = Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Install Portal:", PortalName))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("FirstName:", FirstName))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("LastName:", LastName))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Username:", Username))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Email:", Email))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Description:", Description))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("Keywords:", KeyWords))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("TemplatePath:", TemplatePath))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("TemplateFile:", TemplateFile))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("HomeDirectory:", HomeDirectory))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("PortalAlias:", PortalAlias))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("ServerPath:", ServerPath))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("ChildPath:", ChildPath))
                            objEventLogInfo.LogProperties.Add(New DotNetNuke.Services.Log.EventLog.LogDetailInfo("IsChildPortal:", IsChildPortal.ToString()))
                            Dim objEventLog As New Services.Log.EventLog.EventLogController
                            objEventLog.AddLog(objEventLogInfo)
                        Catch ex As Exception
                            ' error
                        End Try
                    Else
                        Throw New Exception(strMessage)
                    End If
                Else    ' clean up
                    DeletePortalInfo(intPortalId)
                    intPortalId = -1
                    Throw New Exception(strMessage)
                End If
            Else
                strMessage += Localization.GetString("CreatePortal.Error")
                Throw New Exception(strMessage)
            End If

            Return intPortalId

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Deletes a portal permanently
        ''' </summary>
        ''' <param name="PortalId">PortalId of the portal to be deleted</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	03/09/2004	Created
        ''' 	[VMasanas]	26/10/2004	Remove dependent data (skins, modules)
        '''     [cnurse]    24/11/2006  Removal of Modules moved to sproc
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub DeletePortalInfo(ByVal PortalId As Integer)
            ' remove skin assignments
            SkinController.SetSkin(SkinInfo.RootSkin, PortalId, SkinType.Portal, "")
            SkinController.SetSkin(SkinInfo.RootContainer, PortalId, SkinType.Portal, "")
            SkinController.SetSkin(SkinInfo.RootSkin, PortalId, SkinType.Admin, "")
            SkinController.SetSkin(SkinInfo.RootContainer, PortalId, SkinType.Admin, "")

            'delete portal users
            UserController.DeleteUsers(PortalId, False, True)

            'delete portal
            DataProvider.Instance().DeletePortalInfo(PortalId)

            ' clear portal alias cache and entire portal
            DataCache.ClearHostCache(True)

        End Sub

        Public Shared Function GetCurrentPortalSettings() As PortalSettings
            Return CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets information of a portal
        ''' </summary>
        ''' <param name="PortalId">Id of the portal</param>
        ''' <returns>PortalInfo object with portal definition</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetPortal(ByVal PortalId As Integer) As PortalInfo
            Dim key As String = String.Format(DataCache.PortalCacheKey, PortalId)

            'First Check the Portal Cache
            Dim portal As PortalInfo = TryCast(DataCache.GetPersistentCacheItem(key, GetType(PortalInfo)), PortalInfo)

            If portal Is Nothing Then
                'portal caching settings
                Dim timeOut As Int32 = DataCache.PortalCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get tabs form Database
                Dim dr As IDataReader = DataProvider.Instance().GetPortal(PortalId)
                Try
                    portal = FillPortalInfo(dr)
                Finally
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try

                'Cache tabs
                If timeOut > 0 Then
                    DataCache.SetCache(key, portal, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If

            Return portal
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets information from all portals
        ''' </summary>
        ''' <returns>ArrayList of PortalInfo objects</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetPortals() As ArrayList
            Return FillPortalInfoCollection(DataProvider.Instance().GetPortals())
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the space used at the host level
        ''' </summary>
        ''' <returns>Space used in bytes</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	19/04/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetPortalSpaceUsedBytes() As Long
            Return GetPortalSpaceUsedBytes(-1)
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the space used by a portal in bytes
        ''' </summary>
        ''' <param name="PortalId">Id of the portal</param>
        ''' <returns>Space used in bytes</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	07/04/2006	Created
        '''     [VMasanas]  11/05/2006  Use file size stored on the db. This is necessary
        '''         to take into account the new secure file storage options
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function GetPortalSpaceUsedBytes(ByVal portalId As Integer) As Long
            Dim size As Long = 0

            Dim dr As IDataReader = DataProvider.Instance().GetPortalSpaceUsed(portalId)
            If dr.Read Then
                If Not IsDBNull(dr("SpaceUsed")) Then
                    size = Convert.ToInt64(dr("SpaceUsed"))
                End If
            End If
            dr.Close()

            Return size
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Verifies if there's enough space to upload a new file on the given portal
        ''' </summary>
        ''' <param name="PortalId">Id of the portal</param>
        ''' <param name="fileSizeBytes">Size of the file being uploaded</param>
        ''' <returns>True if there's enough space available to upload the file</returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	19/04/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Function HasSpaceAvailable(ByVal portalId As Integer, ByVal fileSizeBytes As Long) As Boolean

            Dim hostSpace As Integer

            If portalId = -1 Then
                hostSpace = 0
            Else
                Dim ps As PortalSettings = GetCurrentPortalSettings()
                If Not ps Is Nothing And ps.PortalId = portalId Then
                    hostSpace = ps.HostSpace
                Else
                    Dim portal As PortalInfo = GetPortal(portalId)
                    hostSpace = portal.HostSpace
                End If
            End If

            Return (((GetPortalSpaceUsedBytes(portalId) + fileSizeBytes) / 1024 ^ 2) <= hostSpace) Or hostSpace = 0

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processess a template file for the new portal. This method will be called twice: for the portal template and for the admin template
        ''' </summary>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <param name="TemplatePath">Path for the folder where templates are stored</param>
        ''' <param name="TemplateFile">Template file to process</param>
        ''' <param name="AdministratorId">UserId for the portal administrator. This is used to assign roles to this user</param>
        ''' <param name="mergeTabs">Flag to determine whether Module content is merged.</param>
        ''' <param name="IsNewPortal">Flag to determine is the template is applied to an existing portal or a new one.</param>
        ''' <remarks>
        ''' The roles and settings nodes will only be processed on the portal template file.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	27/08/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub ParseTemplate(ByVal PortalId As Integer, ByVal TemplatePath As String, ByVal TemplateFile As String, ByVal AdministratorId As Integer, ByVal mergeTabs As PortalTemplateModuleAction, ByVal IsNewPortal As Boolean)

            Dim xmlDoc As New XmlDocument
            Dim node As XmlNode
            Dim AdministratorRoleId As Integer = -1
            Dim RegisteredRoleId As Integer = -1
            Dim SubscriberRoleId As Integer = -1
            Dim objrole As New RoleController
            Dim isAdminTemplate As Boolean

            isAdminTemplate = (TemplateFile = "admin.template")

            ' open the XML file
            Try
                xmlDoc.Load(TemplatePath & TemplateFile)
            Catch    ' error
                ' 
            End Try

            ' settings, roles, folders and files can only be specified in portal templates, will be ignored on the admin template
            If Not isAdminTemplate Then

                ' parse roles if available
                node = xmlDoc.SelectSingleNode("//portal/roles")
                If Not node Is Nothing Then
                    ParseRoles(node, PortalId, AdministratorId, AdministratorRoleId, RegisteredRoleId, SubscriberRoleId)
                End If

                ' create required roles if not already created
                If AdministratorRoleId = -1 Then
                    AdministratorRoleId = CreateRole(PortalId, "Administrators", "Portal Administrators", 0, 0, "M", 0, 0, "N", False, False)
                End If
                If RegisteredRoleId = -1 Then
                    RegisteredRoleId = CreateRole(PortalId, "Registered Users", "Registered Users", 0, 0, "M", 0, 0, "N", False, True)
                End If
                If SubscriberRoleId = -1 Then
                    SubscriberRoleId = CreateRole(PortalId, "Subscribers", "A public role for portal subscriptions", 0, 0, "M", 0, 0, "N", True, True)
                End If

                objrole.AddUserRole(PortalId, AdministratorId, AdministratorRoleId, Null.NullDate, Null.NullDate)
                objrole.AddUserRole(PortalId, AdministratorId, RegisteredRoleId, Null.NullDate, Null.NullDate)
                objrole.AddUserRole(PortalId, AdministratorId, SubscriberRoleId, Null.NullDate, Null.NullDate)

                ' parse portal folders
                node = xmlDoc.SelectSingleNode("//portal/folders")
                If Not node Is Nothing Then
                    ParseFolders(node, PortalId)
                End If
                ' force creation of root folder if not present on template
                Dim objController As New FolderController
                If objController.GetFolder(PortalId, "", False) Is Nothing Then
                    Dim folderid As Integer = objController.AddFolder(PortalId, "", Services.FileSystem.FolderController.StorageLocationTypes.InsecureFileSystem, True, False)
                    Dim objPermissionController As New Permissions.PermissionController
                    Dim arr As ArrayList = objPermissionController.GetPermissionByCodeAndKey("SYSTEM_FOLDER", "")
                    For Each objpermission As Permissions.PermissionInfo In arr
                        FileSystemUtils.SetFolderPermission(PortalId, folderid, objpermission.PermissionID, AdministratorRoleId, "")
                        If objpermission.PermissionKey = "READ" Then
                            ' add READ permissions to the All Users Role
                            FileSystemUtils.SetFolderPermission(PortalId, folderid, objpermission.PermissionID, Integer.Parse(glbRoleAllUsers), "")
                        End If
                    Next
                End If

                ' parse portal settings if available only for new portals
                node = xmlDoc.SelectSingleNode("//portal/settings")
                If Not node Is Nothing And IsNewPortal Then
                    ParsePortalSettings(node, PortalId)
                End If

                ' update portal setup
                Dim objportal As PortalInfo
                objportal = GetPortal(PortalId)
                UpdatePortalSetup(PortalId, AdministratorId, AdministratorRoleId, RegisteredRoleId, objportal.SplashTabId, objportal.HomeTabId, objportal.LoginTabId, objportal.UserTabId, objportal.AdminTabId)

                'Remove Exising Tabs if doing a "Replace"
                If mergeTabs = PortalTemplateModuleAction.Replace Then
                    Dim objTabs As New TabController
                    Dim objTab As TabInfo
                    For Each tabPair As KeyValuePair(Of Integer, TabInfo) In objTabs.GetTabsByPortal(PortalId)
                        objTab = tabPair.Value
                        If Not objTab.IsAdminTab Then
                            'soft delete Tab
                            objTab.TabName = objTab.TabName & "_old"
                            objTab.IsDeleted = True
                            objTabs.UpdateTab(objTab)
                            'Delete all Modules
                            Dim objModules As New ModuleController
                            Dim objModule As ModuleInfo
                            For Each modulePair As KeyValuePair(Of Integer, ModuleInfo) In objModules.GetTabModules(objTab.TabID)
                                objModule = modulePair.Value
                                objModules.DeleteTabModule(objModule.TabID, objModule.ModuleID)
                            Next
                        End If
                    Next
                End If
            End If

            ' parse portal tabs
            node = xmlDoc.SelectSingleNode("//portal/tabs")
            If Not node Is Nothing Then
                ParseTabs(node, PortalId, isAdminTemplate, mergeTabs, IsNewPortal)
            End If

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes the resource file for the template file selected
        ''' </summary>
        ''' <param name="portalPath">New portal's folder</param>
        ''' <param name="TemplateFile">Selected template file</param>
        ''' <remarks>
        ''' The resource file is a zip file with the same name as the selected template file and with
        ''' an extension of .resources (to unable this file being downloaded).
        ''' For example: for template file "portal.template" a resource file "portal.template.resources" can be defined.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	10/09/2004	Created
        '''     [cnurse]    11/08/2004  Moved from SignUp to PortalController
        '''     [cnurse]    03/04/2005  made Public
        '''     [cnurse]    05/20/2005  moved most of processing to new method in FileSystemUtils
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub ProcessResourceFile(ByVal portalPath As String, ByVal TemplateFile As String)

            Dim objZipInputStream As ZipInputStream
            Try
                objZipInputStream = New ZipInputStream(New FileStream(TemplateFile & ".resources", FileMode.Open, FileAccess.Read))
                FileSystemUtils.UnzipResources(objZipInputStream, portalPath)
            Catch exc As Exception
                ' error opening file
            End Try

        End Sub

        Public Sub UpdatePortalExpiry(ByVal PortalId As Integer)

            Dim ExpiryDate As DateTime

            Dim dr As IDataReader = DataProvider.Instance().GetPortal(PortalId)
            If dr.Read Then
                If IsDBNull(dr("ExpiryDate")) Then
                    ExpiryDate = Convert.ToDateTime(dr("ExpiryDate"))
                Else
                    ExpiryDate = Now()
                End If

                DataProvider.Instance().UpdatePortalInfo(PortalId, Convert.ToString(dr("PortalName")), Convert.ToString(dr("LogoFile")), Convert.ToString(dr("FooterText")), DateAdd(DateInterval.Month, 1, ExpiryDate), Convert.ToInt32(dr("UserRegistration")), Convert.ToInt32(dr("BannerAdvertising")), Convert.ToString(dr("Currency")), Convert.ToInt32(dr("AdministratorId")), Convert.ToDouble(dr("HostFee")), Convert.ToDouble(dr("HostSpace")), Convert.ToInt32(dr("PageQuota")), Convert.ToInt32(dr("UserQuota")), Convert.ToString(dr("PaymentProcessor")), Convert.ToString(dr("ProcessorUserId")), Convert.ToString(dr("ProcessorPassword")), Convert.ToString(dr("Description")), Convert.ToString(dr("KeyWords")), Convert.ToString(dr("BackgroundFile")), Convert.ToInt32(dr("SiteLogHistory")), Convert.ToInt32(dr("SplashTabId")), Convert.ToInt32(dr("HomeTabId")), Convert.ToInt32(dr("LoginTabId")), Convert.ToInt32(dr("UserTabId")), Convert.ToString(dr("DefaultLanguage")), Convert.ToInt32(dr("TimeZoneOffset")), Convert.ToString(dr("HomeDirectory")))
            End If
            dr.Close()

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates basic portal information
        ''' </summary>
        ''' <param name="Portal"></param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/13/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub UpdatePortalInfo(ByVal Portal As PortalInfo)

            UpdatePortalInfo(Portal.PortalID, Portal.PortalName, _
             Portal.LogoFile, Portal.FooterText, Portal.ExpiryDate, Portal.UserRegistration, _
             Portal.BannerAdvertising, Portal.Currency, Portal.AdministratorId, _
             Portal.HostFee, Portal.HostSpace, Portal.PageQuota, Portal.UserQuota, Portal.PaymentProcessor, Portal.ProcessorUserId, _
             Portal.ProcessorPassword, Portal.Description, Portal.KeyWords, _
             Portal.BackgroundFile, Portal.SiteLogHistory, Portal.SplashTabId, Portal.HomeTabId, _
             Portal.LoginTabId, Portal.UserTabId, Portal.DefaultLanguage, Portal.TimeZoneOffset, Portal.HomeDirectory)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Updates basic portal information
        ''' </summary>
        ''' <param name="PortalId"></param>
        ''' <param name="PortalName"></param>
        ''' <param name="LogoFile"></param>
        ''' <param name="FooterText"></param>
        ''' <param name="ExpiryDate"></param>
        ''' <param name="UserRegistration"></param>
        ''' <param name="BannerAdvertising"></param>
        ''' <param name="Currency"></param>
        ''' <param name="AdministratorId"></param>
        ''' <param name="HostFee"></param>
        ''' <param name="HostSpace"></param>
        ''' <param name="PaymentProcessor"></param>
        ''' <param name="ProcessorUserId"></param>
        ''' <param name="ProcessorPassword"></param>
        ''' <param name="Description"></param>
        ''' <param name="KeyWords"></param>
        ''' <param name="BackgroundFile"></param>
        ''' <param name="SiteLogHistory"></param>
        ''' <param name="HomeTabId"></param>
        ''' <param name="LoginTabId"></param>
        ''' <param name="UserTabId"></param>
        ''' <param name="DefaultLanguage"></param>
        ''' <param name="TimeZoneOffset"></param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub UpdatePortalInfo(ByVal PortalId As Integer, ByVal PortalName As String, ByVal LogoFile As String, ByVal FooterText As String, ByVal ExpiryDate As Date, ByVal UserRegistration As Integer, ByVal BannerAdvertising As Integer, ByVal Currency As String, ByVal AdministratorId As Integer, ByVal HostFee As Double, ByVal HostSpace As Double, ByVal PageQuota As Integer, ByVal UserQuota As Integer, ByVal PaymentProcessor As String, ByVal ProcessorUserId As String, ByVal ProcessorPassword As String, ByVal Description As String, ByVal KeyWords As String, ByVal BackgroundFile As String, ByVal SiteLogHistory As Integer, ByVal SplashTabId As Integer, ByVal HomeTabId As Integer, ByVal LoginTabId As Integer, ByVal UserTabId As Integer, ByVal DefaultLanguage As String, ByVal TimeZoneOffset As Integer, ByVal HomeDirectory As String)

            DataProvider.Instance().UpdatePortalInfo(PortalId, PortalName, LogoFile, FooterText, ExpiryDate, UserRegistration, BannerAdvertising, Currency, AdministratorId, HostFee, HostSpace, PageQuota, UserQuota, PaymentProcessor, ProcessorUserId, ProcessorPassword, Description, KeyWords, BackgroundFile, SiteLogHistory, SplashTabId, HomeTabId, LoginTabId, UserTabId, DefaultLanguage, TimeZoneOffset, HomeDirectory)

            ' clear portal settings
            DataCache.ClearPortalCache(PortalId, True)

        End Sub

#End Region

#Region "Obsolete Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the space used by a portal in bytes
        ''' </summary>
        ''' <param name="PortalId">Id of the portal</param>
        '''  <returns>Space used in bytes</returns>
        ''' <remarks>
        ''' If PortalId is -1 or not  present (defaults to -1) the host space (\Portals\_default\) will be returned.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	27/08/2004	Changed to return the real space used by a portal looking at the files on disc.
        ''' 	[VMasanas]	07/04/2006	Marked as obsolete (dnn 3.3). Will return Interger.Max if size > 2 GB (int limit)
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Obsolete("This function has been replaced by GetPortalSpaceUsedBytes")> _
        Public Function GetPortalSpaceUsed(Optional ByVal portalId As Integer = -1) As Integer
            Dim size As Integer
            Try
                size = Convert.ToInt32(GetPortalSpaceUsedBytes(portalId))
            Catch ex As Exception
                size = Integer.MaxValue
            End Try

            Return size
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Processes all panes and modules in the template file
        ''' </summary>
        ''' <param name="nodePanes">Template file node for the panes is current tab</param>
        ''' <param name="PortalId">PortalId of the new portal</param>
        ''' <param name="TabId">Tab being processed</param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	03/09/2004	Created
        ''' 	[VMasanas]	15/10/2004	Modified for new skin structure
        '''		[cnurse]	15/10/2004	Modified to allow for merging template
        '''								with existing pages
        ''' </history>
        ''' -----------------------------------------------------------------------------
        <Obsolete("This function has been replaced by TabController.DeserializePanes")> _
        Public Sub ParsePanes(ByVal nodePanes As XmlNode, ByVal PortalId As Integer, ByVal TabId As Integer, ByVal mergeTabs As PortalTemplateModuleAction, ByVal hModules As Hashtable)
            TabController.DeserializePanes(nodePanes, PortalId, TabId, mergeTabs, hModules)
        End Sub

#End Region

    End Class

End Namespace
