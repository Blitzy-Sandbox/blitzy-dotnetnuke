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
Imports System.Drawing
Imports System.Reflection
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports System.IO

Imports DotNetNuke.Common.Globals
Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Services.Localization

Namespace DotNetNuke.UI.WebControls

    ''' -----------------------------------------------------------------------------
    ''' Namespace:  DotNetNuke.UI.WebControls
    ''' Project:    DotNetNuke
    ''' Class:      CommandButton
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The CommandButton Class provides an enhanced Button control for DotNetNuke
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	01/06/2006	Created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    <ToolboxData("<{0}:CommandButton runat=server></{0}:CommandButton>")> _
    Public Class CommandButton
        Inherits WebControl
        Implements INamingContainer

#Region "Controls"

        Private link As New LinkButton
        Private icon As New ImageButton

#End Region

#Region "Private Members"

        Private _onClick As String
        Private _resourceKey As String

#End Region

#Region "Events Declarations"

        Public Event Click As EventHandler

#End Region

#Region "Public Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets whether the control causes Validation to occur
        ''' </summary>
        ''' <remarks>Defaults to True</remarks>
        ''' <value>A Boolean</value>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property CausesValidation() As Boolean
            Get
                Me.EnsureChildControls()
                Return link.CausesValidation
            End Get
            Set(ByVal Value As Boolean)
                Me.EnsureChildControls()
                icon.CausesValidation = Value
                link.CausesValidation = Value
            End Set
        End Property

        Public Property CommandName() As String
            Get
                Me.EnsureChildControls()
                Return link.CommandName
            End Get
            Set(ByVal Value As String)
                Me.EnsureChildControls()
                icon.CommandName = Value
                link.CommandName = Value
            End Set
        End Property
        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets whether the link is displayed
        ''' </summary>
        ''' <remarks>Defaults to True</remarks>
        ''' <value>A Boolean</value>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property DisplayLink() As Boolean
            Get
                Me.EnsureChildControls()
                Return link.Visible
            End Get
            Set(ByVal Value As Boolean)
                Me.EnsureChildControls()
                link.Visible = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets whether the icon is displayed
        ''' </summary>
        ''' <remarks>Defaults to True</remarks>
        ''' <value>A Boolean</value>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property DisplayIcon() As Boolean
            Get
                Me.EnsureChildControls()
                Return icon.Visible
            End Get
            Set(ByVal Value As Boolean)
                Me.EnsureChildControls()
                icon.Visible = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the Image used for the Icon
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property ImageUrl() As String
            Get
                Me.EnsureChildControls()
                Return icon.ImageUrl
            End Get
            Set(ByVal Value As String)
                Me.EnsureChildControls()
                icon.ImageUrl = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the "onClick" Attribute
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property OnClick() As String
            Get
                Me.EnsureChildControls()
                Return link.Attributes.Item("onclick")
            End Get
            Set(ByVal Value As String)
                Me.EnsureChildControls()
                If Value = "" Then
                    icon.Attributes.Remove("onclick")
                    link.Attributes.Remove("onclick")
                Else
                    icon.Attributes.Add("onclick", Value)
                    link.Attributes.Add("onclick", Value)
                End If
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the Resource Key used for the Control
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property ResourceKey() As String
            Get
                Me.EnsureChildControls()
                Return link.Attributes.Item("resourcekey")
            End Get
            Set(ByVal Value As String)
                Me.EnsureChildControls()
                If Value = "" Then
                    icon.Attributes.Remove("resourcekey")
                    link.Attributes.Remove("resourcekey")
                Else
                    icon.Attributes.Add("resourcekey", Value)
                    link.Attributes.Add("resourcekey", Value)
                End If
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the Text used for the Control
        ''' </summary>
        ''' <value>A String</value>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property Text() As String
            Get
                Me.EnsureChildControls()
                Return link.Text
            End Get
            Set(ByVal Value As String)
                Me.EnsureChildControls()
                icon.ToolTip = Value
                link.Text = Value
                link.ToolTip = Value
            End Set
        End Property

#End Region

#Region "Protected Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CreateChildControls overrides the Base class's method to correctly build the
        ''' control based on the configuration
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Protected Overrides Sub CreateChildControls()

            If CssClass = "" Then CssClass = "CommandButton"

            icon.Visible = True
            icon.CausesValidation = True
            AddHandler icon.Click, AddressOf RaiseImageClick
            Me.Controls.Add(icon)

            Me.Controls.Add(New LiteralControl("&nbsp;"))

            link.Visible = True
            link.CausesValidation = True
            AddHandler link.Click, AddressOf RaiseClick
            Me.Controls.Add(link)

            If DisplayIcon = True And ImageUrl <> "" Then
                icon.EnableViewState = Me.EnableViewState
            End If

            If DisplayLink Then
                link.CssClass = CssClass
                link.EnableViewState = Me.EnableViewState
            End If

        End Sub

#End Region

#Region "Event Handlers"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' RaiseClick runs when one of the contained Link buttons is clciked
        ''' </summary>
        ''' <remarks>It raises a Click Event.
        ''' </remarks>
        ''' <param name="sender"> The object that triggers the event</param>
        ''' <param name="e">An EventArgs object</param>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub RaiseClick(ByVal sender As Object, ByVal e As EventArgs)

            RaiseEvent Click(Me, e)

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' RaiseImageClick runs when the Image button is clicked
        ''' </summary>
        ''' <remarks>It raises a Command Event.
        ''' </remarks>
        ''' <param name="sender"> The object that triggers the event</param>
        ''' <param name="e">An ImageClickEventArgs object</param>
        ''' <history>
        ''' 	[cnurse]	01/06/2006	Created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub RaiseImageClick(ByVal sender As Object, ByVal e As ImageClickEventArgs)

            RaiseEvent Click(Me, New EventArgs)

        End Sub

#End Region

    End Class

End Namespace

