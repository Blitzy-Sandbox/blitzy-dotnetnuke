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
Imports System.Web.UI.WebControls
Imports DotNetNuke
Imports System.Reflection
Imports System.IO
Imports DotNetNuke.Entities.Tabs
Imports System.Xml
Imports DotNetNuke.Services.Localization
Imports DotNetNuke.Services.FileSystem

Namespace DotNetNuke.Modules.Admin.Tabs

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Partial Class Import
        Inherits Entities.Modules.PortalModuleBase

#Region "Private Members"

        Private _Tab As TabInfo

#End Region

#Region "Public Properties"

        Public ReadOnly Property Tab() As TabInfo
            Get
                If _Tab Is Nothing Then
                    Dim objTabs As New TabController
                    _Tab = objTabs.GetTab(TabId, PortalId, False)
                End If
                Return _Tab
            End Get
        End Property

#End Region

#Region "Private Members"

        Private Shadows ModuleId As Integer = -1

#End Region

#Region "Event Handlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
            Try
                If Not Page.IsPostBack Then
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
                    Next
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
            cboTemplate.Items.Clear()
            If cboFolders.SelectedIndex <> 0 Then
                Dim arrFiles As ArrayList = Common.Globals.GetFileList(PortalId, "page.template", False, cboFolders.SelectedItem.Value)
                Dim objFile As FileItem
                For Each objFile In arrFiles
                    cboTemplate.Items.Add(New ListItem(objFile.Text.Replace(".page.template", ""), objFile.Text))
                Next
                cboTemplate.Items.Insert(0, New ListItem(Localization.GetString("None_Specified"), "-1"))
                cboTemplate.SelectedIndex = 0
            End If
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Sub cmdCancel_Click(ByVal sender As Object, ByVal e As EventArgs) Handles cmdCancel.Click
            Try
                Response.Redirect(NavigateURL(), True)
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
        Protected Sub cmdImport_Click(ByVal sender As Object, ByVal e As EventArgs) Handles cmdImport.Click
            Try
                If cboTemplate.SelectedItem Is Nothing Then
                    UI.Skins.Skin.AddModuleMessage(Me, Localization.GetString("SpecifyFile", LocalResourceFile), UI.Skins.Controls.ModuleMessage.ModuleMessageType.RedError)
                    Exit Sub
                End If
                If optMode.SelectedIndex = -1 Then
                    UI.Skins.Skin.AddModuleMessage(Me, Localization.GetString("SpecifyMode", LocalResourceFile), UI.Skins.Controls.ModuleMessage.ModuleMessageType.RedError)
                    Exit Sub
                End If

                'Load template
                Dim xmlDoc As New XmlDocument
                xmlDoc.Load(PortalSettings.HomeDirectoryMapPath & cboFolders.SelectedValue & cboTemplate.SelectedValue)

                Dim nodeTab As XmlNode = xmlDoc.SelectSingleNode("//portal/tabs/tab")
                Dim objTab As TabInfo
                If optMode.SelectedValue = "ADD" Then
                    'New Tab
                    objTab = TabController.DeserializeTab(txtTabName.Text, nodeTab, PortalId)
                Else
                    'Replace Existing Tab
                    objTab = TabController.DeserializeTab(nodeTab, Tab, PortalId, PortalTemplateModuleAction.Replace)
                End If

                If optRedirect.SelectedValue = "VIEW" Then
                    Response.Redirect(NavigateURL(objTab.TabID), True)
                Else
                    Response.Redirect(NavigateURL(objTab.TabID, "Tab", "action=edit"), True)
                End If

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

        Protected Sub cboTemplate_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles cboTemplate.SelectedIndexChanged
            Try
                Dim filename As String

                If cboTemplate.SelectedIndex > 0 Then
                    filename = PortalSettings.HomeDirectoryMapPath & cboFolders.SelectedItem.Value & cboTemplate.SelectedValue
                    Dim xmldoc As New XmlDocument
                    Dim node As XmlNode
                    xmldoc.Load(filename)
                    node = xmldoc.SelectSingleNode("//portal/description")
                    If Not node Is Nothing AndAlso node.InnerXml <> "" Then
                        lblTemplateDescription.Visible = True
                        lblTemplateDescription.Text = Server.HtmlDecode(node.InnerXml)
                        txtTabName.Text = cboTemplate.SelectedItem.Text
                    Else
                        lblTemplateDescription.Visible = False
                    End If
                Else
                    lblTemplateDescription.Visible = False
                End If
            Catch exc As Exception           'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try

        End Sub

        Protected Sub optMode_SelectedIndexChanged(ByVal sender As Object, ByVal e As System.EventArgs) Handles optMode.SelectedIndexChanged
            trTabName.Visible = (optMode.SelectedIndex = 0)
        End Sub

#End Region

    End Class

End Namespace
