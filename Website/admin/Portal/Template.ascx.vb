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
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports System.Xml
Imports System.Xml.Serialization
Imports DotNetNuke
Imports System.IO
Imports DotNetNuke.Security.Roles
Imports DotNetNuke.UI.Skins
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Definitions
Imports ICSharpCode.SharpZipLib.Zip
Imports ICSharpCode.SharpZipLib.Checksums
Imports DotNetNuke.Entities.Profile
Imports DotNetNuke.Common.Lists


Namespace DotNetNuke.Modules.Admin.PortalManagement

	''' -----------------------------------------------------------------------------
	''' <summary>
	''' The Template PortalModuleBase is used to export a Portal as a Template
	''' </summary>
    ''' <remarks>
	''' </remarks>
	''' <history>
	''' 	[cnurse]	9/28/2004	Updated to reflect design changes for Help, 508 support
	'''                       and localisation
	''' </history>
	''' -----------------------------------------------------------------------------
	Partial  Class Template
		Inherits DotNetNuke.Entities.Modules.PortalModuleBase

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Helper method to read skins assigned at the three diferent levels: Portal, Tab, Module
        ''' </summary>
        ''' <param name="xml">Reference to xml document to create new elements</param>
        ''' <param name="nodeToAdd">Node to add the skin information</param>
        ''' <param name="skinRoot">Skin object to get: skins or containers</param>
        ''' <param name="skinLevel">Skin level to get: portal, tab, module</param>
        ''' <param name="id">ID of the object to get the skin. Used in conjunction with <paramref name="skinLevel"/> parameter.
        '''     Ex: if skinLevel is portal, <paramref name="id"/> will be PortalID.
        ''' </param>
        ''' <remarks>
        ''' Skin information nodes are added to <paramref name="nodeToAdd"/> node.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	23/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub AddSkinXml(ByVal xml As XmlDocument, ByVal nodeToAdd As XmlNode, ByVal skinRoot As String, ByVal skinLevel As String, ByVal id As Integer)
            Dim newnode As System.Xml.XmlElement
            Dim skin As UI.Skins.SkinInfo
            Dim elementprefix As String

            If skinRoot = SkinInfo.RootSkin Then
                elementprefix = "skin"
            Else
                elementprefix = "container"
            End If

            Select Case skinLevel
                Case "portal"
                    skin = SkinController.GetSkin(skinRoot, id, SkinType.Portal)
                    If Not skin Is Nothing Then
                        newnode = xml.CreateElement(elementprefix + "src")
                        newnode.InnerText = skin.SkinSrc
                        nodeToAdd.AppendChild(newnode)
                    End If
                    skin = SkinController.GetSkin(skinRoot, id, SkinType.Admin)
                    If Not skin Is Nothing Then
                        newnode = xml.CreateElement(elementprefix + "srcadmin")
                        newnode.InnerText = skin.SkinSrc
                        nodeToAdd.AppendChild(newnode)
                    End If
            End Select
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Serializes all Files
        ''' </summary>
        ''' <param name="xmlTemplate">Reference to XmlDocument context</param>
        ''' <param name="nodeFiles">Node to add the serialized objects</param>
        ''' <param name="objportal">Portal to serialize</param>
        ''' <param name="folderPath">The folder containing the files</param>
        ''' <remarks>
        ''' The serialization uses the xml attributes defined in FileInfo class.
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	Created
        '''     [cnurse]    05/20/2004  Extracted adding of file to zip to new FileSystemUtils method
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SerializeFiles(ByVal xmlTemplate As XmlDocument, ByVal nodeFiles As XmlNode, ByVal objportal As PortalInfo, ByVal folderPath As String, ByRef zipFile As ZipOutputStream)
            Dim xser As XmlSerializer
            Dim sw As StringWriter
            Dim nodeFile As XmlNode
            Dim xmlFile As XmlDocument
            Dim objFile As Services.FileSystem.FileInfo
            Dim objFiles As New Services.FileSystem.FileController
            Dim objFolders As New Services.FileSystem.FolderController
            Dim objFolder As Services.FileSystem.FolderInfo = objFolders.GetFolder(objportal.PortalID, folderPath)
            Dim arrFiles As ArrayList = FileSystemUtils.GetFilesByFolder(objportal.PortalID, objFolder.FolderID)
            Dim filePath As String

            xser = New XmlSerializer(GetType(Services.FileSystem.FileInfo))

            For Each objFile In arrFiles
                ' verify that the file exists on the file system
                filePath = objportal.HomeDirectoryMapPath & folderPath & objFile.FileName
                If File.Exists(filePath) Then
                    sw = New StringWriter
                    xser.Serialize(sw, objFile)

                    'Add node to template
                    xmlFile = New XmlDocument
                    xmlFile.LoadXml(sw.GetStringBuilder().ToString())
                    nodeFile = xmlFile.SelectSingleNode("file")
                    nodeFile.Attributes.Remove(nodeFile.Attributes.ItemOf("xmlns:xsd"))
                    nodeFile.Attributes.Remove(nodeFile.Attributes.ItemOf("xmlns:xsi"))
                    nodeFiles.AppendChild(xmlTemplate.ImportNode(nodeFile, True))

                    FileSystemUtils.AddToZip(zipFile, filePath, objFile.FileName, folderPath)

                End If
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Serializes all Folders including Permissions
        ''' </summary>
        ''' <param name="xmlTemplate">Reference to XmlDocument context</param>
        ''' <param name="objportal">Portal to serialize</param>
        ''' <remarks>
        ''' The serialization uses the xml attributes defined in FolderInfo class.
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SerializeFolders(ByVal xmlTemplate As XmlDocument, ByVal nodeFolders As XmlNode, ByVal objportal As PortalInfo, ByRef zipFile As ZipOutputStream)
            Dim xser As XmlSerializer
            Dim sw As StringWriter
            Dim nodeFolder As XmlNode
            Dim xmlFolder As XmlDocument

            ' Sync db and filesystem before exporting so all required files are found
            FileSystemUtils.Synchronize(objportal.PortalID, objportal.AdministratorRoleId, objportal.HomeDirectoryMapPath)

            Dim objFolder As Services.FileSystem.FolderInfo
            Dim objFolders As New Services.FileSystem.FolderController
            Dim arrFolders As ArrayList = objFolders.GetFoldersByPortal(objportal.PortalID)

            xser = New XmlSerializer(GetType(Services.FileSystem.FolderInfo))
            For Each objFolder In arrFolders
                sw = New StringWriter
                xser.Serialize(sw, objFolder)

                xmlFolder = New XmlDocument
                xmlFolder.LoadXml(sw.GetStringBuilder().ToString())
                nodeFolder = xmlFolder.SelectSingleNode("folder")
                nodeFolder.Attributes.Remove(nodeFolder.Attributes.ItemOf("xmlns:xsd"))
                nodeFolder.Attributes.Remove(nodeFolder.Attributes.ItemOf("xmlns:xsi"))

                'Serialize Folder Permissions
                Dim nodePermissions As XmlNode
                nodePermissions = xmlTemplate.CreateElement("folderpermissions")
                SerializeFolderPermissions(xmlTemplate, nodePermissions, objportal, objFolder.FolderPath)
                nodeFolder.AppendChild(xmlFolder.ImportNode(nodePermissions, True))

                ' Serialize files
                Dim nodeFiles As XmlNode
                nodeFiles = xmlTemplate.CreateElement("files")
                SerializeFiles(xmlTemplate, nodeFiles, objportal, objFolder.FolderPath, zipFile)
                nodeFolder.AppendChild(xmlFolder.ImportNode(nodeFiles, True))

                nodeFolders.AppendChild(xmlTemplate.ImportNode(nodeFolder, True))
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Serializes all Folder Permissions
        ''' </summary>
        ''' <param name="xmlTemplate">Reference to XmlDocument context</param>
        ''' <param name="objportal">Portal to serialize</param>
        ''' <param name="folderPath">The folder containing the files</param>
        ''' <remarks>
        ''' The serialization uses the xml attributes defined in FolderInfo class.
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/08/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SerializeFolderPermissions(ByVal xmlTemplate As XmlDocument, ByVal nodePermissions As XmlNode, ByVal objportal As PortalInfo, ByVal folderPath As String)
            Dim nodePermission As XmlElement
            Dim objPermission As DotNetNuke.Security.Permissions.FolderPermissionInfo
            Dim objPermissions As New DotNetNuke.Security.Permissions.FolderPermissionController
            Dim arrPermissions As ArrayList = objPermissions.GetFolderPermissionsByFolder(objportal.PortalID, folderPath)

            For Each objPermission In arrPermissions
                nodePermission = xmlTemplate.CreateElement("permission")
                nodePermission.AppendChild(XmlUtils.CreateElement(xmlTemplate, "permissioncode", objPermission.PermissionCode))
                nodePermission.AppendChild(XmlUtils.CreateElement(xmlTemplate, "permissionkey", objPermission.PermissionKey))
                nodePermission.AppendChild(XmlUtils.CreateElement(xmlTemplate, "rolename", objPermission.RoleName))
                nodePermission.AppendChild(XmlUtils.CreateElement(xmlTemplate, "allowaccess", objPermission.AllowAccess.ToString.ToLower))
                nodePermissions.AppendChild(nodePermission)
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Serializes all Profile Definitions
        ''' </summary>
        ''' <param name="xmlTemplate">Reference to XmlDocument context</param>
        ''' <param name="nodeProfileDefinitions">Node to add the serialized objects</param>
        ''' <param name="objportal">Portal to serialize</param>
        ''' <remarks>
        ''' The serialization uses the xml attributes defined in ProfilePropertyDefinition class.
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SerializeProfileDefinitions(ByVal xmlTemplate As XmlDocument, ByVal nodeProfileDefinitions As XmlNode, ByVal objportal As PortalInfo)
            Dim xser As XmlSerializer
            Dim sw As StringWriter
            Dim newnode As XmlNode
            Dim nodeProfileDefinition As XmlNode
            Dim xmlPropertyDefinition As XmlDocument

            Dim objListController As New ListController
            Dim objList As ListEntryInfo

            xser = New XmlSerializer(GetType(ProfilePropertyDefinition))
            For Each objProfileProperty As ProfilePropertyDefinition In ProfileController.GetPropertyDefinitionsByPortal(objportal.PortalID, False)
                sw = New StringWriter
                xser.Serialize(sw, objProfileProperty)

                xmlPropertyDefinition = New XmlDocument
                xmlPropertyDefinition.LoadXml(sw.GetStringBuilder().ToString())
                nodeProfileDefinition = xmlPropertyDefinition.SelectSingleNode("profiledefinition")
                nodeProfileDefinition.Attributes.Remove(nodeProfileDefinition.Attributes.ItemOf("xmlns:xsd"))
                nodeProfileDefinition.Attributes.Remove(nodeProfileDefinition.Attributes.ItemOf("xmlns:xsi"))
                objList = objListController.GetListEntryInfo(objProfileProperty.DataType)
                newnode = xmlPropertyDefinition.CreateElement("datatype")
                If objList Is Nothing Then
                    newnode.InnerXml = "Unknown"
                Else
                    newnode.InnerXml = objList.Value
                End If
                nodeProfileDefinition.AppendChild(newnode)
                nodeProfileDefinitions.AppendChild(xmlTemplate.ImportNode(nodeProfileDefinition, True))
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Serializes all Roles
        ''' </summary>
        ''' <param name="xmlTemplate">Reference to XmlDocument context</param>
        ''' <param name="nodeRoles">Node to add the serialized objects</param>
        ''' <param name="objportal">Portal to serialize</param>
        ''' <returns>A hastable with all serialized roles. Will be used later to translate RoleId to RoleName</returns>
        ''' <remarks>
        ''' The serialization uses the xml attributes defined in RoleInfo class.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	23/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function SerializeRoles(ByVal xmlTemplate As XmlDocument, ByVal nodeRoles As XmlNode, ByVal objportal As PortalInfo) As Hashtable
            Dim xser As XmlSerializer
            Dim sw As StringWriter
            Dim nodeRole As XmlNode
            Dim newnode As XmlNode
            Dim xmlRole As XmlDocument
            Dim objrole As RoleInfo
            Dim objroles As New RoleController
            Dim hRoles As New Hashtable

            xser = New XmlSerializer(GetType(RoleInfo))
            For Each objrole In objroles.GetPortalRoles(objportal.PortalID)
                sw = New StringWriter
                xser.Serialize(sw, objrole)

                xmlRole = New XmlDocument
                xmlRole.LoadXml(sw.GetStringBuilder().ToString())
                nodeRole = xmlRole.SelectSingleNode("role")
                nodeRole.Attributes.Remove(nodeRole.Attributes.ItemOf("xmlns:xsd"))
                nodeRole.Attributes.Remove(nodeRole.Attributes.ItemOf("xmlns:xsi"))
                If objrole.RoleID = objportal.AdministratorRoleId Then
                    newnode = xmlRole.CreateElement("roletype")
                    newnode.InnerXml = "adminrole"
                    nodeRole.AppendChild(newnode)
                End If
                If objrole.RoleID = objportal.RegisteredRoleId Then
                    newnode = xmlRole.CreateElement("roletype")
                    newnode.InnerXml = "registeredrole"
                    nodeRole.AppendChild(newnode)
                End If
                If objrole.RoleName = "Subscribers" Then
                    newnode = xmlRole.CreateElement("roletype")
                    newnode.InnerXml = "subscriberrole"
                    nodeRole.AppendChild(newnode)
                End If
                nodeRoles.AppendChild(xmlTemplate.ImportNode(nodeRole, True))
                ' save role, we'll need them later
                hRoles.Add(objrole.RoleID.ToString, objrole.RoleName)
            Next

            ' Add default DNN roles
            hRoles.Add(glbRoleAllUsers, "All")
            hRoles.Add(glbRoleUnauthUser, "Unauthenticated")
            hRoles.Add(glbRoleSuperUser, "Super")

            Return hRoles
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Serializes a PortalInfo object
        ''' </summary>
        ''' <param name="xmlTemplate">Reference to XmlDocument context</param>
        ''' <param name="nodePortal">Node to add the serialized objects</param>
        ''' <param name="objportal">Portal to serialize</param>
        ''' <remarks>
        ''' The serialization uses the xml attributes defined in PortalInfo class.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	23/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SerializeSettings(ByVal xmlTemplate As XmlDocument, ByVal nodePortal As XmlNode, ByVal objportal As PortalInfo)
            Dim xser As XmlSerializer
            Dim sw As StringWriter
            Dim nodeSettings As XmlNode
            Dim xmlSettings As XmlDocument

            xser = New XmlSerializer(GetType(PortalInfo))
            sw = New StringWriter
            xser.Serialize(sw, objportal)

            xmlSettings = New XmlDocument
            xmlSettings.LoadXml(sw.GetStringBuilder().ToString())
            nodeSettings = xmlSettings.SelectSingleNode("settings")
            nodeSettings.Attributes.Remove(nodeSettings.Attributes.ItemOf("xmlns:xsd"))
            nodeSettings.Attributes.Remove(nodeSettings.Attributes.ItemOf("xmlns:xsi"))
            'remove unwanted elements
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("portalid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("portalname"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("administratorid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("administratorroleid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("administratorrolename"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("registeredroleid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("registeredrolename"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("description"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("keywords"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("processorpassword"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("processoruserid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("admintabid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("supertabid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("users"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("pages"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("splashtabid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("hometabid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("logintabid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("usertabid"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("homedirectory"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("expirydate"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("currency"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("hostfee"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("hostspace"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("pagequota"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("userquota"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("backgroundfile"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("paymentprocessor"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("siteloghistory"))
            nodeSettings.RemoveChild(nodeSettings.SelectSingleNode("email"))

            AddSkinXml(xmlSettings, nodeSettings, SkinInfo.RootSkin, "portal", objportal.PortalID)
            AddSkinXml(xmlSettings, nodeSettings, SkinInfo.RootContainer, "portal", objportal.PortalID)
            nodePortal.AppendChild(xmlTemplate.ImportNode(nodeSettings, True))
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Serializes all portal Tabs
        ''' </summary>
        ''' <param name="xmlTemplate">Reference to XmlDocument context</param>
        ''' <param name="nodeTabs">Node to add the serialized objects</param>
        ''' <param name="objportal">Portal to serialize</param>
        ''' <remarks>
        ''' Only portal tabs will be exported to the template, Admin tabs are not exported.
        ''' On each tab, all modules will also be exported.
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	23/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub SerializeTabs(ByVal xmlTemplate As XmlDocument, ByVal nodeTabs As XmlNode, ByVal objportal As PortalInfo)
            Dim nodeTab As XmlNode
            Dim xmlTab As XmlDocument
            Dim objtab As TabInfo
            Dim objtabs As New TabController

            'supporting object to build the tab hierarchy
            Dim hTabs As New Hashtable

            For Each objtab In objtabs.GetTabs(objportal.PortalID)
                'if not an admin tab & not deleted
                If objtab.TabOrder < 10000 And Not objtab.IsDeleted Then
                    'Serialize the Tab
                    xmlTab = New XmlDocument()
                    nodeTab = TabController.SerializeTab(xmlTab, hTabs, objtab, objportal, chkContent.Checked)
                    nodeTabs.AppendChild(xmlTemplate.ImportNode(nodeTab, True))
                End If
            Next
        End Sub

#End Region

#Region "EventHandlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Page_Load runs when the control is loaded
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="e"></param>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	23/09/2004	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
            Try
                If Not Page.IsPostBack Then
                    Dim objportals As New PortalController
                    cboPortals.DataTextField = "PortalName"
                    cboPortals.DataValueField = "PortalId"
                    cboPortals.DataSource = objportals.GetPortals()
                    cboPortals.DataBind()
                End If

            Catch exc As Exception
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Exports the selected portal
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="e"></param>
        ''' <remarks>
        ''' Template will be saved in Portals\_default folder.
        ''' An extension of .template will be added to filename if not entered
        ''' </remarks>
        ''' <history>
        ''' 	[VMasanas]	23/09/2004	Created
        ''' 	[cnurse]	11/08/2004	Addition of files to template
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub cmdExport_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles cmdExport.Click
            Try
                Dim xmlTemplate As XmlDocument
                Dim nodePortal As XmlNode
                Dim hRoles As Hashtable
                Dim resourcesFile As ZipOutputStream

                If Not Page.IsValid Then
                    Exit Sub
                End If

                Dim filename As String
				filename = Common.Globals.HostMapPath & txtTemplateName.Text
                If Not filename.EndsWith(".template") Then
                    filename += ".template"
                End If

                xmlTemplate = New XmlDocument
                nodePortal = xmlTemplate.AppendChild(xmlTemplate.CreateElement("portal"))
                nodePortal.Attributes.Append(XmlUtils.CreateAttribute(xmlTemplate, "version", "3.0"))

                'Add template description
                Dim node As XmlElement = xmlTemplate.CreateElement("description")
                node.InnerXml = Server.HtmlEncode(txtDescription.Text)
                nodePortal.AppendChild(node)

                'Serialize portal settings
                Dim objportal As PortalInfo
                Dim objportals As New PortalController
                objportal = objportals.GetPortal(Convert.ToInt32(cboPortals.SelectedValue))
                SerializeSettings(xmlTemplate, nodePortal, objportal)

                ' Serialize Profile Definitions
                Dim nodeProfileDefinitions As XmlNode
                nodeProfileDefinitions = nodePortal.AppendChild(xmlTemplate.CreateElement("profiledefinitions"))
                SerializeProfileDefinitions(xmlTemplate, nodeProfileDefinitions, objportal)

                'Serialize Roles
                Dim nodeRoles As XmlNode
                nodeRoles = nodePortal.AppendChild(xmlTemplate.CreateElement("roles"))
                hRoles = SerializeRoles(xmlTemplate, nodeRoles, objportal)

                ' Serialize tabs
                Dim nodeTabs As XmlNode
                nodeTabs = nodePortal.AppendChild(xmlTemplate.CreateElement("tabs"))
                SerializeTabs(xmlTemplate, nodeTabs, objportal)

                If chkContent.Checked Then

                    'Create Zip File to hold files
                    resourcesFile = New ZipOutputStream(File.Create(filename & ".resources"))
                    resourcesFile.SetLevel(6)

                    ' Serialize folders (while adding files to zip file)
                    Dim nodeFolders As XmlNode
                    nodeFolders = nodePortal.AppendChild(xmlTemplate.CreateElement("folders"))
                    SerializeFolders(xmlTemplate, nodeFolders, objportal, resourcesFile)

                    'Finish and Close Zip file
                    resourcesFile.Finish()
                    resourcesFile.Close()
                End If

                xmlTemplate.Save(filename)
                lblMessage.Text = String.Format(Services.Localization.Localization.GetString("ExportedMessage", Me.LocalResourceFile), filename)

            Catch exc As Exception
                ProcessModuleLoadException(Me, exc)
            End Try

        End Sub

#End Region

    End Class

End Namespace
