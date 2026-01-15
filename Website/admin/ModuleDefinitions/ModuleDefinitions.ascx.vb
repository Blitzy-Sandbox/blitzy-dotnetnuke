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

Imports System.IO
Imports System.XML
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Modules.Actions
Imports DotNetNuke.Services.FileSystem

Namespace DotNetNuke.Modules.Admin.ModuleDefinitions

	''' -----------------------------------------------------------------------------
	''' <summary>
	''' The ModuleDefinitions PortalModuleBase is used to manage the modules
	''' attached to this portal
	''' </summary>
    ''' <remarks>
	''' </remarks>
	''' <history>
	''' 	[cnurse]	9/28/2004	Updated to reflect design changes for Help, 508 support
	'''                       and localisation
	''' </history>
	''' -----------------------------------------------------------------------------
    Partial Class ModuleDefinitions

        Inherits Entities.Modules.PortalModuleBase
        Implements Entities.Modules.IActionable

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' BindData fetches the data from the database and updates the controls
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/28/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub BindData()

            If Upgrade.Upgrade.UpgradeIndicator(glbAppVersion, Request.IsLocal, Request.IsSecureConnection) = "" Then
                lblUpdate.Visible = False
                grdDefinitions.Columns(4).HeaderText = ""
            End If

            ' Get the portal's defs from the database
            Dim objDesktopModules As New DesktopModuleController

            Dim arr As ArrayList = objDesktopModules.GetDesktopModules()

            Dim objDesktopModule As New DesktopModuleInfo

            objDesktopModule.DesktopModuleID = -2
            objDesktopModule.FriendlyName = Services.Localization.Localization.GetString("SkinObjects")
            objDesktopModule.Description = Services.Localization.Localization.GetString("SkinObjectsDescription")
            objDesktopModule.Version = ""
            objDesktopModule.IsPremium = False

            arr.Insert(0, objDesktopModule)

            'Localize Grid
            Services.Localization.Localization.LocalizeDataGrid(grdDefinitions, Me.LocalResourceFile)

            grdDefinitions.DataSource = arr
            grdDefinitions.DataBind()

        End Sub

        Private Sub BindLocales()
            Dim ds As New DataSet
            Dim dv As DataView
            Dim i As Integer
            Dim localeKey As String
            Dim localeName As String


            ds.ReadXml(Server.MapPath(Localization.SupportedLocalesFile))
            dv = ds.Tables(0).DefaultView
            dv.Sort = "name ASC"

            cboLocales.Items.Clear()
            For i = 0 To dv.Count - 1
                localeKey = Convert.ToString(dv(i)("key"))
                Dim cinfo As New CultureInfo(localeKey)

                Try
                    localeName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cinfo.NativeName)
                Catch
                    localeName = Convert.ToString(dv(i)("name")) + " (" + localeKey + ")"
                End Try
                cboLocales.Items.Add(New ListItem(localeName, localeKey))
            Next

            If cboLocales.Items.Count > 0 Then
                cboLocales.SelectedIndex = 0
            End If
        End Sub

        Private Sub BindModules()
            Dim arrFiles As String()
            Dim strFile As String

            Dim InstallPath As String = ApplicationMapPath & "\Install\Module"

            If Directory.Exists(InstallPath) Then
                arrFiles = Directory.GetFiles(InstallPath)
                Dim iFile As Integer = 0
                For Each strFile In arrFiles
                    Dim strResource As String = strFile.Replace(InstallPath + "\", "")
                    If strResource.ToLower <> "placeholder.txt" Then
                        Dim moduleItem As ListItem = New ListItem()
                        moduleItem.Value = strResource
                        strResource = strResource.Replace(".zip", "")
                        strResource = strResource.Replace(".resources", "")
                        strResource = strResource.Replace("_Install", ")")
                        strResource = strResource.Replace("_Source", ")")
                        strResource = strResource.Replace("_", " (")
                        moduleItem.Text = strResource
                        lstModules.Items.Add(moduleItem)
                    End If
                Next
            End If
        End Sub

        Private Sub DeleteFile(ByVal strFile As String)

            ' delete the file
            Try
                File.SetAttributes(strFile, FileAttributes.Normal)
                File.Delete(strFile)
            Catch
                ' error removing the file
            End Try

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UpgradeIndicator returns the imageurl for the upgrade button for the module
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function UpgradeIndicator(ByVal Version As String, ByVal ModuleName As String, ByVal Culture As String) As String
            Dim strURL As String = Upgrade.Upgrade.UpgradeIndicator(Version, ModuleName, Culture, Request.IsLocal, Request.IsSecureConnection)
            If strURL = "" Then
                strURL = Common.Globals.ApplicationPath & "/images/spacer.gif"
            End If
            Return strURL
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' UpgradeRedirect returns the url for the upgrade button for the module
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function UpgradeRedirect(ByVal Version As String, ByVal ModuleName As String, ByVal Culture As String) As String
            Return Upgrade.Upgrade.UpgradeRedirect(Version, ModuleName, Culture)
        End Function

#End Region

#Region "Public Methods"

        Public Function UpgradeService(ByVal Version As String, ByVal ModuleName As String) As String
            Dim strUpgradeService As String = ""
            strUpgradeService += "<a title=""" & Localization.GetString("UpgradeMessage", Me.LocalResourceFile) & """ href=""" & UpgradeRedirect(Version, ModuleName, "") & """ target=""_new""><img title=""" & Localization.GetString("UpgradeMessage", Me.LocalResourceFile) & """ src=""" & UpgradeIndicator(Version, ModuleName, "") & """ border=""0"" /></a>"
            strUpgradeService += "<br />"
            strUpgradeService += "<a title=""" & Localization.GetString("LanguageMessage", Me.LocalResourceFile) & """ href=""" & UpgradeRedirect(Version, ModuleName, cboLocales.SelectedItem.Value) & """ target=""_new""><img title=""" & Localization.GetString("LanguageMessage", Me.LocalResourceFile) & """ src=""" & UpgradeIndicator(Version, ModuleName, cboLocales.SelectedItem.Value) & """ border=""0"" /></a>"
            Return strUpgradeService
        End Function

#End Region

#Region "Event Handlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Page_Load runs when the control is loaded.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	9/28/2004	Updated to reflect design changes for Help, 508 support
        '''                       and localisation
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
            Try

                If Not Page.IsPostBack Then
                    BindLocales()
                    BindModules()
                End If
                BindData()

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try

        End Sub

        Protected Sub grdDefinitions_ItemDataBound(ByVal sender As Object, ByVal e As System.Web.UI.WebControls.DataGridItemEventArgs) Handles grdDefinitions.ItemDataBound
            If e.Item.ItemIndex = 0 Then
                Dim row As DataGridItem = New DataGridItem(0, 0, ListItemType.Item)
                Dim cell1 As TableCell = New TableCell()
                cell1.Text = ""
                row.Cells.Add(cell1)
                Dim cell2 As TableCell = New TableCell()
                If Localization.GetString("AppTitle", Me.LocalResourceFile) = "" Then
                    cell2.Text = glbAppTitle
                Else
                    cell2.Text = Localization.GetString("AppTitle", Me.LocalResourceFile)
                End If
                row.Cells.Add(cell2)
                Dim cell3 As TableCell = New TableCell()
                If Localization.GetString("AppDescription", Me.LocalResourceFile) = "" Then
                    cell3.Text = glbAppDescription
                Else
                    cell3.Text = Localization.GetString("AppDescription", Me.LocalResourceFile)
                End If
                row.Cells.Add(cell3)
                Dim cell4 As TableCell = New TableCell()
                cell4.Text = glbAppVersion
                row.Cells.Add(cell4)
                Dim cell5 As TableCell = New TableCell()
                cell5.Text = UpgradeService(glbAppVersion, "~")
                row.Cells.Add(cell5)
                grdDefinitions.Controls(0).Controls.AddAt(1, row)
            End If
        End Sub

        Protected Sub cmdInstall_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles cmdInstall.Click

            Dim InstallPath As String = ApplicationMapPath & "\Install\Module\"

            For Each moduleItem As ListItem In lstModules.Items
                If moduleItem.Selected Then
                    Dim strFile As String = InstallPath + moduleItem.Value
                    Dim strExtension As String = Path.GetExtension(strFile)

                    If strExtension.ToLower = ".zip" Or strExtension.ToLower = ".resources" Then
                        phPaLogs.Visible = True
                        Dim objPaInstaller As New ResourceInstaller.PaInstaller(strFile, Common.Globals.ApplicationMapPath)
                        objPaInstaller.InstallerInfo.Log.StartJob(Localization.GetString("Installing", LocalResourceFile) + moduleItem.Text)
                        If objPaInstaller.Install() Then
                            ' delete package
                            DeleteFile(strFile)
                        Else
                            ' save error log
                            phPaLogs.Controls.Add(objPaInstaller.InstallerInfo.Log.GetLogsTable)
                        End If
                    End If
                End If
            Next

            If phPaLogs.Controls.Count > 0 Then
                ' display error log
                cmdRefresh.Visible = True
            Else
                ' refresh installed module list
                Response.Redirect(Request.RawUrl)
            End If

        End Sub

        Protected Sub cmdRefresh_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles cmdRefresh.Click
            Response.Redirect(Request.RawUrl(), True)
        End Sub
#End Region

#Region "Optional Interfaces"
        Public ReadOnly Property ModuleActions() As ModuleActionCollection Implements Entities.Modules.IActionable.ModuleActions
            Get
                Dim Actions As New ModuleActionCollection

                ' install new module
                Dim FileManagerModule As ModuleInfo = (New ModuleController).GetModuleByDefinition(Null.NullInteger, "File Manager")
                Dim params(2) As String
                params(0) = "mid=" & FileManagerModule.ModuleID
                params(1) = "ftype=" & UploadType.Module.ToString
                params(2) = "rtab=" & Me.TabId
                Actions.Add(GetNextActionID, Services.Localization.Localization.GetString("ModuleUpload.Action", LocalResourceFile), ModuleActionType.AddContent, "", "", NavigateURL(FileManagerModule.TabID, "Edit", params), False, SecurityAccessLevel.Host, True, False)

                ' create new module
                Actions.Add(GetNextActionID, Services.Localization.Localization.GetString(ModuleActionType.AddContent, LocalResourceFile), ModuleActionType.AddContent, "", "", EditUrl(), False, SecurityAccessLevel.Host, True, False)

                ' import module
                Actions.Add(GetNextActionID, Services.Localization.Localization.GetString("ModuleImport.Action", LocalResourceFile), ModuleActionType.AddContent, "", "", EditUrl("Import"), False, SecurityAccessLevel.Host, True, False)

                Return Actions
            End Get
        End Property
#End Region

    End Class

End Namespace
