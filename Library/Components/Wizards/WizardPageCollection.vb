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

Namespace DotNetNuke.Services.Wizards

    '''-----------------------------------------------------------------------------
    ''' Project		: DotNetNuke
    ''' Class		: WizardPageCollection
    '''-----------------------------------------------------------------------------
    ''' <summary>
    ''' Represents a collection of <see cref="T:DotNetNuke.WizardPage" /> objects.
    ''' </summary>
    ''' <history>
    ''' 	[cnurse]	10/10/2004	created
    ''' </history>
    '''-----------------------------------------------------------------------------
    Public Class WizardPageCollection
        Inherits CollectionBase

#Region "Constructors"

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Initializes a new, empty instance of the <see cref="T:DotNetNuke.WizardPageCollection" /> class.
        ''' </summary>
        ''' <remarks>The default constructor creates an empty collection of <see cref="T:DotNetNuke.WizardPageInfo" />
        '''  objects.</remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Sub New()
            MyBase.New()
        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' Initializes a new instance of the <see cref="T:DotNetNuke.WizardPageCollection" />
        '''  class containing the specified array of <see cref="T:DotNetNuke.WizardPageInfo" /> objects.
        ''' </summary>
        ''' <param name="value">An array of <see cref="T:DotNetNuke.WizardPageInfo" /> objects 
        ''' with which to initialize the collection. </param>
        ''' <remarks>This overloaded constructor copies the <see cref="T:DotNetNuke.WizardPageInfo" />s
        '''  from the indicated array.</remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Sub New(ByVal value As WizardPage())
            MyBase.New()
            AddRange(value)
        End Sub

#End Region

#Region "Properties"

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets or sets the <see cref="T:DotNetNuke.WizardPageCollection" /> at the 
        ''' specified index in the collection.
        ''' <para>
        ''' In VB.Net, this property is the indexer for the <see cref="T:DotNetNuke.WizardPageCollection" /> class.
        ''' </para>
        ''' </summary>
        ''' <param name="index">The index of the collection to access.</param>
        ''' <value>A <see cref="T:DotNetNuke.WizardPage" /> at each valid index.</value>
        ''' <remarks>This method is an indexer that can be used to access the collection.</remarks>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Default Public Property Item(ByVal index As Integer) As WizardPage
            Get
                Return CType(List(index), WizardPage)
            End Get
            Set(ByVal Value As WizardPage)
                List(index) = Value
            End Set
        End Property

#End Region

#Region "Public Mehods"

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Add an element of the specified <see cref="T:DotNetNuke.WizardPage" /> to the end of the collection.
        ''' </summary>
        ''' <param name="value">An object of type <see cref="T:DotNetNuke.WizardPage" /> to add to the collection.</param>
        ''' <returns>The index of the newly added <see cref="T:DotNetNuke.WizardPage" /></returns>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Function Add(ByVal value As WizardPage) As Integer
            Return List.Add(value)
        End Function    'Add

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Add an element of the specified <see cref="T:DotNetNuke.WizardPage" /> to the end of the collection.
        ''' </summary>
        ''' <param name="title">This is the title that will be displayed for this action</param>
        ''' <param name="icon">This is the identifier to use for this action.</param>
        ''' <param name="ctl">The Control Assocaited with the Page</param>
        ''' <param name="help">The Help text for the  Page</param>
        ''' <returns>The index of the newly added <see cref="T:DotNetNuke.WizardPage" /></returns>
        ''' <remarks>This method creates a new <see cref="T:DotNetNuke.WizardPage" /> with the specified
        ''' values, adds it to the collection and returns the index of the newly created WizardPage.</remarks>
        ''' <history>
        ''' 	[Joe] 	10/18/2003	Created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Function Add(ByVal title As String, ByVal icon As String, ByVal ctl As Control, ByVal help As String) As WizardPage
            Dim page As WizardPage = New WizardPage(title, icon, ctl, help)
            Me.Add(page)
            Return page
        End Function

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets the index in the collection of the specified <see cref="T:DotNetNuke.WizardPage" />, 
        ''' if it exists in the collection.
        ''' </summary>
        ''' <param name="value">The <see cref="T:DotNetNuke.WizardPage" /> to locate in the collection.</param>
        ''' <returns>The index in the collection of the specified object, if found; otherwise, -1.</returns>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Function IndexOf(ByVal value As WizardPage) As Integer
            Return List.IndexOf(value)
        End Function

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Add an element of the specified <see cref="T:DotNetNuke.WizardPage" /> to the 
        ''' collection at the designated index.
        ''' </summary>
        ''' <param name="index">An <see cref="T:system.int32">Integer</see> to indicate the location to add the object to the collection.</param>
        ''' <param name="value">An object of type <see cref="T:DotNetNuke.WizardPage" /> to add to the collection.</param>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Sub Insert(ByVal index As Integer, ByVal value As WizardPage)
            List.Insert(index, value)
        End Sub    'Insert

        '''----------------------------------------------------------------------------- 
        ''' <summary>
        ''' Remove the specified object of type <see cref="T:DotNetNuke.WizardPage" /> from the collection.
        ''' </summary>
        ''' <param name="value">An object of type <see cref="T:DotNetNuke.WizardPage" /> to remove from the collection.</param>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Sub Remove(ByVal value As WizardPage)
            List.Remove(value)
        End Sub

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Gets a value indicating whether the collection contains the specified <see cref="T:DotNetNuke.WizardPage" />.
        ''' </summary>
        ''' <param name="value">The <see cref="T:DotNetNuke.WizardPage" /> to search for in the collection.</param>
        ''' <returns><b>true</b> if the collection contains the specified object; otherwise, <b>false</b>.</returns>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Function Contains(ByVal value As WizardPage) As Boolean
            ' If value is not of type WizardPage, this will return false.
            Return List.Contains(value)
        End Function    'Contains

        '''-----------------------------------------------------------------------------
        ''' <summary>
        ''' Copies the elements of the specified <see cref="T:DotNetNuke.WizardPage" />
        '''  array to the end of the collection.
        ''' </summary>
        ''' <param name="value">An array of type <see cref="T:DotNetNuke.WizardPage" />
        '''  containing the objects to add to the collection.</param>
        ''' <history>
        ''' 	[cnurse]	10/10/2004	created
        ''' </history>
        '''-----------------------------------------------------------------------------
        Public Sub AddRange(ByVal value As WizardPage())
            Dim i As Integer
            For i = 0 To value.Length - 1
                Add(value(i))
            Next i
        End Sub

#End Region

    End Class

End Namespace
