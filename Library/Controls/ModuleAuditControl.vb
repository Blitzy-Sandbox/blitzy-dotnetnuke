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

Namespace DotNetNuke.UI.UserControls

    Public MustInherit Class ModuleAuditControl
        Inherits System.Web.UI.UserControl

        Protected lblCreatedBy As System.Web.UI.WebControls.Label

        Private _CreatedDate As String = ""
        Private _CreatedByUser As String = ""

        Private MyFileName As String = "ModuleAuditControl.ascx"


        ' public properties
        Public WriteOnly Property CreatedDate() As String
            Set(ByVal Value As String)
                _CreatedDate = Value
            End Set
        End Property

        Public WriteOnly Property CreatedByUser() As String
            Set(ByVal Value As String)
                _CreatedByUser = Value
            End Set
        End Property

        '*******************************************************
        '
        ' The Page_Load server event handler on this page is used
        ' to populate the role information for the page
        '
        '*******************************************************

        Private Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
            Try

                If Not Page.IsPostBack Then

                    If IsNumeric(_CreatedByUser) Then
                        ' contains a UserID
                        Dim objUsers As New UserController
                        Dim objUser As UserInfo = UserController.GetUser(PortalController.GetCurrentPortalSettings.PortalId, Integer.Parse(_CreatedByUser), False)
                        If Not objUser Is Nothing Then
                            _CreatedByUser = objUser.DisplayName
                        End If
                    End If

                    Dim str As String = Services.Localization.Localization.GetString("UpdatedBy", Services.Localization.Localization.GetResourceFile(Me, MyFileName))
                    lblCreatedBy.Text = String.Format(str, _CreatedByUser, _CreatedDate)

                End If

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

    End Class

End Namespace
