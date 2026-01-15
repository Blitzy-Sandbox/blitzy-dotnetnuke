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

Namespace DotNetNuke.Services.Wizards

#Region "Event Delegates"
    Public Delegate Sub WizardCancelEventHandler(ByVal sender As Object, ByVal we As WizardCancelEventArgs)
    Public Delegate Sub WizardEventHandler(ByVal sender As Object, ByVal we As WizardEventArgs)
#End Region

    Public Enum WizardCommand
        PreviousPage
        NextPage
        Finish
        Cancel
    End Enum

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The Wizard class defines a custom base class inherited by all
    ''' Wizard controls.  Wizard itself inherits from PortalModuleBase.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	10/10/2004	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    <Obsolete("This class has been obsoleted, as .NET provides a Wizard Framework")> _
    Public Class Wizard

        Inherits DotNetNuke.Entities.Modules.PortalModuleBase

#Region "Controls"

        'Header Controls
        Private WithEvents imgIcon As System.Web.UI.WebControls.Image
        Private WithEvents lblTitle As System.Web.UI.WebControls.Label
        Private WithEvents cmdHelp As System.Web.UI.WebControls.LinkButton
        Private WithEvents cmdHelpIcon As System.Web.UI.WebControls.ImageButton

        'Help Panel
        Private WithEvents WizardHelp As System.Web.UI.HtmlControls.HtmlGenericControl
        Private WithEvents WizardHelpPane As System.Web.UI.HtmlControls.HtmlTableCell
        Private WithEvents lblHelpTitle As System.Web.UI.WebControls.Label

        'Footer Controls
        Private WithEvents lblPages As System.Web.UI.WebControls.Label
        Private WithEvents cmdBack As System.Web.UI.WebControls.LinkButton
        Private WithEvents cmdNext As System.Web.UI.WebControls.LinkButton
        Private WithEvents cmdFinish As System.Web.UI.WebControls.LinkButton
        Private WithEvents cmdCancel As System.Web.UI.WebControls.LinkButton
        Private WithEvents cmdBackIcon As System.Web.UI.WebControls.ImageButton
        Private WithEvents cmdNextIcon As System.Web.UI.WebControls.ImageButton
        Private WithEvents cmdFinishIcon As System.Web.UI.WebControls.ImageButton
        Private WithEvents cmdCancelIcon As System.Web.UI.WebControls.ImageButton


#End Region

#Region "Private Members"

        Private m_Pages As WizardPageCollection = New WizardPageCollection
        Private m_SuccessPage As WizardPage
        Private m_FailurePage As WizardPage
        Private m_EnableBack As Boolean = True
        Private m_EnableNext As Boolean = True
        Private m_EnableFinish As Boolean = True

#End Region

#Region "Events Declarations"
        Public Event BeforePageChanged As WizardCancelEventHandler
        Public Event AfterPageChanged As WizardEventHandler
        Public Event FinishWizard As WizardEventHandler
#End Region

#Region "Properties"

        Public Property CurrentPage() As Integer
            Get
                If ViewState("CurrentPage") Is Nothing Then
                    ViewState("CurrentPage") = 0
                End If
                Return DirectCast(ViewState("CurrentPage"), Integer)
            End Get
            Set(ByVal Value As Integer)
                ViewState("CurrentPage") = Value
            End Set
        End Property

        Public ReadOnly Property Pages() As WizardPageCollection
            Get
                Return m_Pages
            End Get
        End Property

        Public Property FinishPage() As Integer
            Get
                If ViewState("FinishPage") Is Nothing Then
                    ViewState("FinishPage") = 0
                End If
                Return DirectCast(ViewState("FinishPage"), Integer)
            End Get
            Set(ByVal Value As Integer)
                ViewState("FinishPage") = Value
            End Set
        End Property

        Public Property SuccessPage() As WizardPage
            Get
                Return m_SuccessPage
            End Get
            Set(ByVal Value As WizardPage)
                m_SuccessPage = Value
            End Set
        End Property

        Public Property EnableBack() As Boolean
            Get
                Return m_EnableBack
            End Get
            Set(ByVal Value As Boolean)
                m_EnableBack = Value
            End Set
        End Property

        Public Property EnableFinish() As Boolean
            Get
                Return m_EnableFinish
            End Get
            Set(ByVal Value As Boolean)
                m_EnableFinish = Value
            End Set
        End Property

        Public Property EnableNext() As Boolean
            Get
                Return m_EnableNext
            End Get
            Set(ByVal Value As Boolean)
                m_EnableNext = Value
            End Set
        End Property

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddCommandButton adds a Button to the Button Panel
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="icon">The ImageButton to Add</param>
        '''	<param name="link">The LinkButton to Add</param>
        '''	<param name="Key">The Resource Key for the Text/Help</param>
        '''	<param name="imageUrl">The Image Url</param>
        '''	<param name="causesValidation">Flag set the button to not cause validation</param>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function AddCommandButton(ByRef icon As ImageButton, ByRef link As LinkButton, ByVal Key As String, ByVal imageUrl As String, ByVal causesValidation As Boolean) As HtmlTable

            'First Create the Container for the Button
            Dim table As New HtmlTable
            Dim row As New HtmlTableRow
            Dim container As New HtmlTableCell
            table.Attributes.Add("class", "WizardButton")

            table.Rows.Add(row)
            row.Cells.Add(container)

            'Set the Icon properties
            icon = New ImageButton
            icon.ImageUrl = "~/images/" & imageUrl
            icon.ToolTip = Services.Localization.Localization.GetString(Key & ".Help")

            'Set the link properties
            link = New System.Web.UI.WebControls.LinkButton
            link.CssClass = "CommandButton"
            link.Text = Services.Localization.Localization.GetString(Key)
            link.ToolTip = Services.Localization.Localization.GetString(Key & ".Help")
            link.CausesValidation = causesValidation

            'Ad the controls to the Container
            container.Controls.Add(icon)
            container.Controls.Add(New LiteralControl("&nbsp;"))
            container.Controls.Add(link)

            'Return the Control
            Return table
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddFooter adds the Footer to the Wizard
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub AddFooter()

            'get Wizard Table
            Dim Wizard As HtmlTable = DirectCast(Me.FindControl("Wizard"), HtmlTable)

            'Create Footer Row
            Dim FooterRow As New HtmlTableRow
            Dim WizardFooter As New HtmlTableCell
            WizardFooter.ColSpan = 2
            WizardFooter.Width = "100%"

            Dim table As New HtmlTable
            table.Width = "100%"
            Dim row As New HtmlTableRow
            Dim cell As HtmlTableCell

            'Add the Page x of y cell
            cell = New HtmlTableCell
            lblPages = New Label
            lblPages.CssClass = "SubHead"
            cell.Align = "Left"
            cell.Width = "200"
            cell.Controls.Add(lblPages)
            row.Cells.Add(cell)

            'Add the Spacer Cell
            cell = New HtmlTableCell
            cell.Width = "160"
            row.Cells.Add(cell)

            'Add the CommandButtons to the Footer
            cell = New HtmlTableCell
            cell.Controls.Add(AddCommandButton(cmdBackIcon, cmdBack, "cmdBack", "lt.gif", False))
            row.Cells.Add(cell)

            cell = New HtmlTableCell
            cell.Controls.Add(AddCommandButton(cmdNextIcon, cmdNext, "cmdNext", "rt.gif", True))
            row.Cells.Add(cell)

            cell = New HtmlTableCell
            cell.Controls.Add(AddCommandButton(cmdFinishIcon, cmdFinish, "cmdFinish", "end.gif", True))
            row.Cells.Add(cell)

            cell = New HtmlTableCell
            cell.Controls.Add(AddCommandButton(cmdCancelIcon, cmdCancel, "cmdCancel", "cancel.gif", False))
            row.Cells.Add(cell)

            table.Rows.Add(row)
            WizardFooter.Controls.Add(table)

            'Add the event Handlers
            AddHandler cmdNext.Command, AddressOf Me.NextPage
            AddHandler cmdNextIcon.Command, AddressOf Me.NextPage
            AddHandler cmdBack.Command, AddressOf Me.PreviousPage
            AddHandler cmdBackIcon.Command, AddressOf Me.PreviousPage
            AddHandler cmdFinish.Command, AddressOf Me.Finish
            AddHandler cmdFinishIcon.Command, AddressOf Me.Finish
            AddHandler cmdCancel.Command, AddressOf Me.Cancel
            AddHandler cmdCancelIcon.Command, AddressOf Me.Cancel

            WizardFooter.Attributes.Add("class", "WizardFooter")
            WizardFooter.Height = "22"
            WizardFooter.VAlign = "middle"
            FooterRow.Cells.Add(WizardFooter)
            Wizard.Rows.Add(FooterRow)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddHeader adds the Header to the Wizard
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub AddHeader()

            Dim Wizard As HtmlTable = DirectCast(Me.FindControl("Wizard"), HtmlTable)
            Dim HeaderRow As New HtmlTableRow

            Dim WizardHeader As New HtmlTableCell
            WizardHeader.ColSpan = 2
            WizardHeader.Width = "100%"

            Dim headerTable As HtmlTable = New HtmlTable
            headerTable.Width = "100%"
            Dim row As HtmlTableRow = New HtmlTableRow
            Dim cell As HtmlTableCell

            cell = New HtmlTableCell
            cell.Width = "40"
            imgIcon = New System.Web.UI.WebControls.Image
            cell.Controls.Add(imgIcon)
            row.Cells.Add(cell)

            cell = New HtmlTableCell
            cell.Width = "15"
            row.Cells.Add(cell)

            cell = New HtmlTableCell
            cell.Width = "400"
            cell.VAlign = "middle"
            lblTitle = New System.Web.UI.WebControls.Label
            lblTitle.CssClass = "Head"
            cell.Controls.Add(lblTitle)
            row.Cells.Add(cell)

            cell = New HtmlTableCell
            cell.Width = "15"
            row.Cells.Add(cell)

            'Add the Help Button to the Header
            cell = New HtmlTableCell
            cell.Align = "right"
            cell.Controls.Add(AddCommandButton(cmdHelpIcon, cmdHelp, "cmdHelp", "help.gif", False))
            row.Cells.Add(cell)

            headerTable.Rows.Add(row)
            WizardHeader.Controls.Add(headerTable)

            'Add the event Handlers
            AddHandler cmdHelp.Command, AddressOf Me.ShowHelp
            AddHandler cmdHelpIcon.Command, AddressOf Me.ShowHelp

            WizardHeader.Attributes.Add("class", "WizardHeader")
            HeaderRow.Cells.Add(WizardHeader)
            Wizard.Rows.Insert(0, HeaderRow)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ConfigureWizard sets up the Wizard Framework (creates the structure and injects
        '''	the buttons
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ConfigureWizard()

            'set up Wizard Table
            Dim Wizard As HtmlTable = DirectCast(Me.FindControl("Wizard"), HtmlTable)
            Wizard.Attributes.Add("class", "Wizard")
            Wizard.CellPadding = 0
            Wizard.CellSpacing = 0
            Wizard.Border = 0

            'set up Wizard Body
            Dim WizardBody As HtmlTableCell = DirectCast(Me.FindControl("WizardBody"), HtmlTableCell)
            WizardBody.Attributes.Add("class", "WizardBody")
            WizardBody.VAlign = "top"

            'set up Help Cell
            Dim WizardRow As HtmlTableRow = DirectCast(WizardBody.Parent, HtmlTableRow)
            WizardHelpPane = New HtmlTableCell
            WizardHelpPane.Attributes.Add("class", "WizardHelp")
            WizardHelpPane.VAlign = "top"
            WizardHelpPane.Visible = False

            Dim table As New HtmlTable
            table.Width = "180"
            Dim row As New HtmlTableRow
            Dim cell As New HtmlTableCell

            ' Add Help Header
            lblHelpTitle = New Label
            lblHelpTitle.CssClass = "SubHead"
            lblHelpTitle.Text = Services.Localization.Localization.GetString("cmdHelp")
            cell.Controls.Add(lblHelpTitle)
            cell.Controls.Add(New LiteralControl("<hr>"))
            row.Cells.Add(cell)
            table.Rows.Add(row)

            ' Add Help Cell
            row = New HtmlTableRow
            cell = New HtmlTableCell
            cell.VAlign = "top"
            WizardHelp = New HtmlGenericControl
            WizardHelp.Attributes.Add("class", "WizardHelpText")
            cell.Controls.Add(WizardHelp)
            row.Cells.Add(cell)
            table.Rows.Add(row)

            WizardHelpPane.Controls.Add(table)
            WizardRow.Cells.Add(WizardHelpPane)

            'Add the Header to the Wizard
            AddHeader()

            'Add the Footer to the Wizard
            AddFooter()

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DisplayHelp shows/hides the Help Panel
        '''	the buttons
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="show">Flag that determines whether the Help Panel is displayed/hidden</param>
        ''' <history>
        ''' 	[cnurse]	11/04/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub DisplayHelp(ByVal show As Boolean)

            If show Then
                Dim wizPage As WizardPage = Me.Pages(CurrentPage)
                lblHelpTitle.Text = Localization.Localization.GetString("Help") & " - " & wizPage.Title
                WizardHelp.InnerHtml = wizPage.Help
            Else
                lblHelpTitle.Text = ""
                WizardHelp.InnerHtml = ""
            End If

            WizardHelpPane.Visible = show
        End Sub

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddPage adds a Wizard Page to the Control
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="title">The Page's Title</param>
        '''	<param name="icon">The Page's Icon</param>
        '''	<param name="ctl">The Page's Control</param>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub AddPage(ByVal title As String, ByVal icon As String, ByVal ctl As Control)

            AddPage(title, icon, ctl, WizardPageType.Content, "")

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddPage adds a Wizard Page to the Control
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="title">The Page's Title</param>
        '''	<param name="icon">The Page's Icon</param>
        '''	<param name="ctl">The Page's Control</param>
        '''	<param name="help">The Page's Help Text</param>
        ''' <history>
        ''' 	[cnurse]	11/03/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub AddPage(ByVal title As String, ByVal icon As String, ByVal ctl As Control, ByVal help As String)

            AddPage(title, icon, ctl, WizardPageType.Content, help)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddPage adds a Wizard Page to the Control
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="title">The Page's Title</param>
        '''	<param name="icon">The Page's Icon</param>
        '''	<param name="ctl">The Page's Control</param>
        '''	<param name="type">The type of the Wizard Page</param>
        '''	<param name="help">The Page's Help Text</param>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        '''     [cnurse]    11/03/2004  Added help parameter
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub AddPage(ByVal title As String, ByVal icon As String, ByVal ctl As Control, ByVal type As WizardPageType, ByVal help As String)

            Select Case type
                Case WizardPageType.Content
                    Me.Pages.Add(title, icon, ctl, help)
                Case WizardPageType.Failure
                    m_FailurePage = New WizardPage(title, icon, ctl, type, help)
                Case WizardPageType.Success
                    m_SuccessPage = New WizardPage(title, icon, ctl, type, help)
            End Select

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DisplayCurrentPage displays the current selected Page
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub DisplayCurrentPage()

            Dim wizPage As WizardPage = DirectCast(Pages(CurrentPage), WizardPage)
            DisplayPage(CurrentPage, wizPage, EnableBack, EnableNext, EnableFinish)

            'Hide any Help that might be visible
            DisplayHelp(False)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DisplayFailurePage displays the Wizards failure page
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub DisplayFailurePage(ByVal message As String)

            'First determine if a custom FailurePage has been set, and load the default
            'if not
            If m_FailurePage Is Nothing Then
                Dim ctlSuccess As WizardSuccess = DirectCast(Me.LoadControl("~/admin/Wizards/Success.ascx"), WizardSuccess)
                ctlSuccess.Message = message
                ctlSuccess.Type = False
                'get Wizard Body
                Dim WizardBody As HtmlTableCell = DirectCast(Me.FindControl("WizardBody"), HtmlTableCell)
                WizardBody.Controls.Add(ctlSuccess)
                m_FailurePage = New WizardPage("Wizard Error", "", ctlSuccess, WizardPageType.Failure)
            End If

            DisplayPage(-1, m_FailurePage, False, False, False)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DisplayPage displays a specific Page
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="wizPage">The Page to display</param>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub DisplayPage(ByVal pageNo As Integer, ByVal wizPage As WizardPage, ByVal ShowBack As Boolean, ByVal ShowNext As Boolean, ByVal ShowFinish As Boolean)

            Dim iPage As Integer
            Dim ctl As Control

            If wizPage Is Nothing Then
                wizPage = New WizardPage
            End If

            'Set the Wizard Body
            For iPage = 0 To Pages.Count - 1
                Dim pge As WizardPage = Pages(iPage)
                ctl = pge.Control
                ctl.Visible = False
            Next
            ctl = wizPage.Control
            ctl.Visible = True

            'Set the Icons ImageUrl
            If wizPage.Icon = "" Then
                imgIcon.ImageUrl = "~/images/1x1.gif"
            Else
                imgIcon.ImageUrl = wizPage.Icon
            End If

            'Set the Titles Text and Style
            lblTitle.Text = wizPage.Title

            'Show/Hide the Back/Next/Finish Buttons
            If (CurrentPage > 0) And ShowBack Then
                cmdBack.Enabled = True
                cmdBackIcon.Enabled = True
            Else
                cmdBack.Enabled = False
                cmdBackIcon.Enabled = False
            End If
            If (CurrentPage < m_Pages.Count - 1) And ShowNext Then
                cmdNext.Enabled = True
                cmdNextIcon.Enabled = True
            Else
                cmdNext.Enabled = False
                cmdNextIcon.Enabled = False
            End If
            If (CurrentPage >= FinishPage) And ShowFinish Then
                cmdFinish.Enabled = True
                cmdFinishIcon.Enabled = True
            Else
                cmdFinish.Enabled = False
                cmdFinishIcon.Enabled = False
            End If

            'Set the Help
            If wizPage.Help = "" Then
                cmdHelp.Enabled = False
                cmdHelpIcon.Enabled = False
            Else
                cmdHelp.Enabled = True
                cmdHelpIcon.Enabled = True
            End If

            'Set the Pages
            lblPages.Text = String.Format(Services.Localization.Localization.GetString("Pages"), CurrentPage + 1, Pages.Count)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' DisplaySuccessPage displays the Wizards success page
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/09/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub DisplaySuccessPage(ByVal message As String)

            'First determine if a custom SuccessPage has been set, and load the default
            'if not
            If m_SuccessPage Is Nothing Then
                Dim ctlSuccess As WizardSuccess = DirectCast(Me.LoadControl("~/admin/Wizards/Success.ascx"), WizardSuccess)
                ctlSuccess.Message = message
                'get Wizard Body
                Dim WizardBody As HtmlTableCell = DirectCast(Me.FindControl("WizardBody"), HtmlTableCell)
                WizardBody.Controls.Add(ctlSuccess)
                m_SuccessPage = New WizardPage("Congratulations", "", ctlSuccess, WizardPageType.Success)
            End If

            DisplayPage(-1, m_SuccessPage, False, False, False)

            'Hide any Help that might be visible
            DisplayHelp(False)

            'Disable Cancel to prevent ViewState issues
            cmdCancel.Enabled = True
            cmdCancelIcon.Enabled = True

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' EnableCommand allows a Wizard to Enable/Disable any of the four Command
        '''	options
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="cmd">The WizardCommand to Enable/Display</param>
        '''	<param name="enable">A flag that determines whether the Commadn is Enabled (true)
        '''	or Disabled (False)</param>
        ''' <history>
        ''' 	[cnurse]	10/12/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub EnableCommand(ByVal cmd As WizardCommand, ByVal enable As Boolean)

            Select Case cmd
                Case WizardCommand.Cancel
                    cmdCancel.Enabled = enable
                Case WizardCommand.Finish
                    cmdFinish.Enabled = enable
                Case WizardCommand.NextPage
                    cmdNext.Enabled = enable
                Case WizardCommand.PreviousPage
                    cmdBack.Enabled = enable
            End Select

        End Sub

#End Region

#Region "Event Implementations"

        Protected Overridable Sub OnAfterPageChanged(ByVal e As WizardEventArgs)
            'Invokes the delegates.
            RaiseEvent AfterPageChanged(Me, e)
        End Sub

        Protected Overridable Sub OnBeforePageChanged(ByVal e As WizardCancelEventArgs)
            'Invokes the delegates.
            RaiseEvent BeforePageChanged(Me, e)
        End Sub

        Protected Overridable Sub OnFinishWizard(ByVal e As WizardEventArgs)
            'Invokes the delegates.
            RaiseEvent FinishWizard(Me, e)
        End Sub

#End Region

#Region "Event Handlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Page_Init runs when the Control is Constructed
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Page_Init(ByVal sender As Object, ByVal e As System.EventArgs) Handles MyBase.Init

            ConfigureWizard()

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Page_Load runs when the Control is loaded
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/2/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles MyBase.Load

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Cancel runs when the Cancel Button is clicked and returns the user to the
        ''' Previous Location
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/2/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Cancel(ByVal sender As Object, ByVal e As System.Web.UI.WebControls.CommandEventArgs)
            Try
                Response.Redirect("~/" & glbDefaultPage, True)

            Catch exc As Exception    'Module failed to load
                ProcessModuleLoadException(Me, exc)
            End Try
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Finish runs when the Back Button is clicked and raises a FinishWizard
        '''	Event
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub Finish(ByVal sender As Object, ByVal e As System.Web.UI.WebControls.CommandEventArgs)

            'Create a New WizardEventArgs Object and trigger a Finish Event
            Dim we As WizardEventArgs = New WizardEventArgs(CurrentPage, Pages)
            OnFinishWizard(we)


        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' NextPage runs when the Next Button is clicked and raises a NextPage
        '''	Event
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub NextPage(ByVal sender As Object, ByVal e As System.Web.UI.WebControls.CommandEventArgs)

            'If we are not on the last page Create a New WizardEventArgs Object 
            'and trigger a Previous Page Event
            If CurrentPage < Pages.Count - 1 Then
                Dim PrevPage As Integer = CurrentPage
                Dim wce As WizardCancelEventArgs = New WizardCancelEventArgs(CurrentPage, PrevPage, Pages)
                OnBeforePageChanged(wce)
                If wce.Cancel = False Then
                    'Event Not Cancelled by Event Consumer
                    CurrentPage += 1
                    Dim we As WizardEventArgs = New WizardEventArgs(CurrentPage, PrevPage, Pages)
                    OnAfterPageChanged(we)
                End If
            End If

            DisplayCurrentPage()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' PreviousPage runs when the Back Button is clicked and raises a PreviousPage
        '''	Event
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub PreviousPage(ByVal sender As Object, ByVal e As System.Web.UI.WebControls.CommandEventArgs)

            If CurrentPage > 0 Then
                Dim PrevPage As Integer = CurrentPage
                Dim wce As WizardCancelEventArgs = New WizardCancelEventArgs(CurrentPage, PrevPage, Pages)
                OnBeforePageChanged(wce)
                If wce.Cancel = False Then
                    'Event Not Cancelled by Event Consumer
                    CurrentPage -= 1
                    Dim we As WizardEventArgs = New WizardEventArgs(CurrentPage, PrevPage, Pages)
                    OnAfterPageChanged(we)
                End If
            End If

            DisplayCurrentPage()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' ShowHelp runs when the Back Buttn is clicked
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/3/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub ShowHelp(ByVal sender As Object, ByVal e As System.Web.UI.WebControls.CommandEventArgs)

            DisplayHelp(Not WizardHelpPane.Visible)

        End Sub

#End Region

    End Class

End Namespace
