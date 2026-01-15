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
Imports System.Web.UI
Imports System.Web.UI.WebControls

Namespace DotNetNuke.Services.Wizards

    Public Enum WizardPageType
        Content
        Success
        Failure
    End Enum

    '''-----------------------------------------------------------------------------
    ''' Project		: DotNetNuke
    ''' Class		: WizardPage
    '''-----------------------------------------------------------------------------
    ''' <summary>
    ''' The WizardPage class defines a Wizard Page.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	10/10/2004	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class WizardPage

#Region "Private Members"

        Private _Control As Control
        Private _Help As String
        Private _Icon As String
        Private _PageType As WizardPageType
        Private _Title As String

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Default Constructor Builds a default WizardPageInfo object.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New()
            Me.New(Services.Localization.Localization.GetString("NoPage"), "", Nothing, WizardPageType.Content, "")
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructor Builds a custom WizardPageInfo object.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="title">The title of the Page</param>
        '''	<param name="icon">The icon for the Page</param>
        '''	<param name="ctl">The control associated with the Page</param>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal title As String, ByVal icon As String, ByVal ctl As Control)
            Me.New(title, icon, ctl, WizardPageType.Content, "")
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructor Builds a custom WizardPageInfo object.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="title">The title of the Page</param>
        '''	<param name="icon">The icon for the Page</param>
        '''	<param name="ctl">The control associated with the Page</param>
        ''' <param name="help">The Help text for the  Page</param>
        ''' <history>
        ''' 	[cnurse]	11/3/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal title As String, ByVal icon As String, ByVal ctl As Control, ByVal help As String)
            Me.New(title, icon, ctl, WizardPageType.Content, help)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructor Builds a custom WizardPageInfo object.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="title">The title of the Page</param>
        '''	<param name="icon">The icon for the Page</param>
        '''	<param name="ctl">The control associated with the Page</param>
        '''	<param name="type">The type of the Page</param>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal title As String, ByVal icon As String, ByVal ctl As Control, ByVal type As WizardPageType)
            Me.New(title, icon, ctl, type, "")
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructor Builds a custom WizardPageInfo object.
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="title">The title of the Page</param>
        '''	<param name="icon">The icon for the Page</param>
        '''	<param name="ctl">The control associated with the Page</param>
        '''	<param name="type">The type of the Page</param>
        ''' <param name="help">The Help text for the  Page</param>
        ''' <history>
        ''' 	[cnurse]	11/3/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal title As String, ByVal icon As String, ByVal ctl As Control, ByVal type As WizardPageType, ByVal help As String)
            _Control = ctl
            _Help = help
            _Icon = icon
            _PageType = type
            _Title = title
        End Sub
#End Region

#Region "Properties"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets And Sets the Panel associated with the Wizard Page.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/12/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property Control() As Control
            Get
                Return _Control
            End Get
            Set(ByVal Value As Control)
                _Control = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets And Sets the Help associated with the Wizard Page.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/3/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property Help() As String
            Get
                Return _Help
            End Get
            Set(ByVal Value As String)
                _Help = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets And Sets the Icon associated with the Wizard Page.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/12/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property Icon() As String
            Get
                Return _Icon
            End Get
            Set(ByVal Value As String)
                _Icon = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets And Sets the Type of the Wizard Page.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/13/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property PageType() As WizardPageType
            Get
                Return _PageType
            End Get
            Set(ByVal Value As WizardPageType)
                _PageType = Value
            End Set
        End Property

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets And Sets the Title of the Wizard Page.
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/12/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Property Title() As String
            Get
                Return _Title
            End Get
            Set(ByVal Value As String)
                _Title = Value
            End Set
        End Property
#End Region

    End Class

End Namespace
