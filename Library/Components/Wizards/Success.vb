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
Imports DotNetNuke.Services

Namespace DotNetNuke.Services.Wizards

	''' -----------------------------------------------------------------------------
	''' <summary>
	''' The WizardSuccess UserControl provides a default success page for the Wizard
	'''	Framework
	''' </summary>
    ''' <remarks>
	''' </remarks>
	''' <history>
	''' 	[cnurse]	10/13/2004	created
	''' </history>
	''' -----------------------------------------------------------------------------
	Public Class WizardSuccess
		Inherits System.Web.UI.UserControl

#Region "Controls"

		Protected WithEvents lblTitle As System.Web.UI.WebControls.Label
		Protected WithEvents lblDetail As System.Web.UI.WebControls.Label
		Protected WithEvents lblMessage As System.Web.UI.WebControls.Label

#End Region

#Region "Private Members"

		Private MyFileName As String = "Success.ascx"
		Private m_Message As String = ""
		Private m_Type As Boolean = True		  'True=Success,  False=Failure

#End Region

#Region "Properties"

		Public Property Message() As String
			Get
				Return m_Message
			End Get
			Set(ByVal Value As String)
				m_Message = Value
			End Set
		End Property

		Public Property Type() As Boolean
			Get
				Return m_Type
			End Get
			Set(ByVal Value As Boolean)
				m_Type = Value
			End Set
		End Property

#End Region

#Region "Event Handlers"

		''' -----------------------------------------------------------------------------
		''' <summary>
		''' Page_PreRender runs just before the control is rendered
		''' </summary>
		''' <remarks>
		''' </remarks>
		''' <history>
		''' 	[cnurse]	10/13/2004	created
		''' </history>
		''' -----------------------------------------------------------------------------
		Private Sub Page_PreRender(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.PreRender

			If m_Type Then
				lblTitle.Text = Localization.Localization.GetString("Wizard.Success.SuccessTitle")
				lblDetail.Text = Localization.Localization.GetString("Wizard.Success.SuccessDetail")
				lblMessage.CssClass = "WizardText"
			Else
				lblTitle.Text = Localization.Localization.GetString("Wizard.Success.FailureTitle")
				lblDetail.Text = Localization.Localization.GetString("Wizard.Success.FailureDetail")
				lblMessage.CssClass = "NormalRed"
			End If

			lblMessage.Text = m_Message
		End Sub

#End Region

#Region " Web Form Designer Generated Code "

		'This call is required by the Web Form Designer.
		<System.Diagnostics.DebuggerStepThrough()> Private Sub InitializeComponent()

		End Sub

		'NOTE: The following placeholder declaration is required by the Web Form Designer.
		'Do not delete or move it.
		Private designerPlaceholderDeclaration As System.Object

		Private Sub Page_Init(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Init
			'CODEGEN: This method call is required by the Web Form Designer
			'Do not modify it using the code editor.
			InitializeComponent()
		End Sub

#End Region

	End Class

End Namespace
