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

Namespace DotNetNuke.Services.Wizards

    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The WizardEventArgs class extends EventArgs to provide Wizard specific
    '''	Properties.
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    ''' 	[cnurse]	10/10/2004	created
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class WizardEventArgs
        Inherits EventArgs

#Region "Private Members"

        Private m_CurPageNo As Integer = 0
        Private m_PrevPageNo As Integer = -1
        Private m_Pages As WizardPageCollection

#End Region

#Region "Constructors"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructs a default WizardEventArgs object
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New()
            Me.New(-1, -1, Nothing)
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructs a custom WizardEventArgs object
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="pageNo">The Page No where the Event happened</param>
        '''	<param name="pages">The WizardPageCollection</param>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal pageNo As Integer, ByVal pages As WizardPageCollection)
            m_CurPageNo = pageNo
            m_PrevPageNo = pageNo
            m_Pages = pages
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Constructs a custom WizardEventArgs object
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        '''	<param name="pageNo">The Page No where the Event happened</param>
        '''	<param name="prevNo">The Page No where the Event happened</param>
        '''	<param name="pages">The WizardPageCollection</param>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Sub New(ByVal pageNo As Integer, ByVal prevNo As Integer, ByVal pages As WizardPageCollection)
            m_CurPageNo = pageNo
            m_PrevPageNo = prevNo
            m_Pages = pages
        End Sub

#End Region

#Region "Properties"

        Public ReadOnly Property Page() As WizardPage
            Get
                Dim retValue As WizardPage = Nothing
                If m_CurPageNo > -1 Then
                    retValue = m_Pages(m_CurPageNo)
                End If
                Return retValue
            End Get
        End Property

        Public ReadOnly Property PageNo() As Integer
            Get
                Return m_CurPageNo
            End Get
        End Property

        Public ReadOnly Property PreviousPage() As WizardPage
            Get
                Dim retValue As WizardPage = Nothing
                If m_PrevPageNo > -1 Then
                    retValue = m_Pages(m_PrevPageNo)
                End If
                Return retValue
            End Get
        End Property

        Public ReadOnly Property PreviousPageNo() As Integer
            Get
                Return m_PrevPageNo
            End Get
        End Property

#End Region

    End Class

End Namespace
