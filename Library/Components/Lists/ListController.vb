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
Imports System.Configuration
Imports System.Data

Namespace DotNetNuke.Common.Lists

    Public Class ListController

#Region "Private Methods"

        Private Sub ClearCache(ByVal PortalId As Integer)
            DataCache.ClearListsCache(PortalId)
        End Sub

        Private Function FillListInfo(ByVal dr As IDataReader, ByVal CheckForOpenDataReader As Boolean) As ListInfo
            Dim objListInfo As ListInfo = Nothing
            ' read datareader
            Dim canContinue As Boolean = True
            If CheckForOpenDataReader Then
                canContinue = False
                If dr.Read Then
                    canContinue = True
                End If
            End If

            If canContinue Then
                objListInfo = New ListInfo(Convert.ToString(dr("ListName")))
                With objListInfo
                    .Level = Convert.ToInt32(dr("Level"))
                    .PortalID = Convert.ToInt32(dr("PortalID"))
                    .DefinitionID = Convert.ToInt32(dr("DefinitionID"))
                    .EntryCount = Convert.ToInt32(dr("EntryCount"))
                    .ParentID = Convert.ToInt32(dr("ParentID"))
                    .ParentKey = Convert.ToString(dr("ParentKey"))
                    .Parent = Convert.ToString(dr("Parent"))
                    .ParentList = Convert.ToString(dr("ParentList"))
                    .EnableSortOrder = (Convert.ToInt32(dr("MaxSortOrder")) > 0)
                    .SystemList = Convert.ToBoolean(dr("SystemList"))
                End With
            End If
            Return objListInfo

        End Function

        Private Function FillListInfoDictionary(ByVal dr As IDataReader) As Dictionary(Of String, ListInfo)
            Dim dic As New Dictionary(Of String, ListInfo)
            Try
                Dim obj As ListInfo
                While dr.Read
                    ' fill business object
                    obj = FillListInfo(dr, False)

                    If Not dic.ContainsKey(obj.Key) Then
                        dic.Add(obj.Key, obj)
                    End If

                End While
            Catch exc As Exception
                LogException(exc)
            Finally
                ' close datareader
                If Not dr Is Nothing Then
                    dr.Close()
                End If
            End Try

            Return dic
        End Function

        Private Function GetListInfoDictionary(ByVal PortalId As Integer) As Dictionary(Of String, ListInfo)

            'Get the Cache Key
            Dim key As String = String.Format(DataCache.ListsCacheKey, PortalId)

            'Try fetching the Dictionary from the Cache
            Dim dicListInfo As Dictionary(Of String, ListInfo) = CType(DataCache.GetCache(key), Dictionary(Of String, ListInfo))

            If dicListInfo Is Nothing Then
                'lists caching settings
                Dim timeOut As Int32 = DataCache.ListsCacheTimeOut * Convert.ToInt32(Common.Globals.PerformanceSetting)

                'Get the Dictionary from the database
                dicListInfo = FillListInfoDictionary(DataProvider.Instance().GetLists(PortalId))

                'Cache the Dictionary
                If timeOut > 0 Then
                    DataCache.SetCache(key, dicListInfo, TimeSpan.FromMinutes(timeOut), True)
                End If
            End If

            'Return the Dictionary
            Return dicListInfo

        End Function

#End Region

#Region "Public Methods"

        Public Function AddListEntry(ByVal ListEntry As ListEntryInfo) As Integer
            Dim EnableSortOrder As Boolean = (ListEntry.SortOrder > 0)
            ClearCache(ListEntry.PortalID)
            Return DataProvider.Instance().AddListEntry(ListEntry.ListName, ListEntry.Value, ListEntry.Text, ListEntry.ParentID, ListEntry.Level, EnableSortOrder, ListEntry.DefinitionID, ListEntry.Description, ListEntry.PortalID)
        End Function

        Public Sub DeleteList(ByVal ListName As String, ByVal ParentKey As String)
            Dim list As ListInfo = GetListInfo(ListName, ParentKey)

            DataProvider.Instance().DeleteList(ListName, ParentKey)
            ClearCache(list.PortalID)
        End Sub

        Public Sub DeleteListEntryByID(ByVal EntryID As Integer, ByVal DeleteChild As Boolean)
            Dim entry As ListEntryInfo = GetListEntryInfo(EntryID)
            DataProvider.Instance().DeleteListEntryByID(EntryID, DeleteChild)
            ClearCache(entry.PortalID)
        End Sub

        Public Sub DeleteListEntryByListName(ByVal ListName As String, ByVal Value As String, ByVal DeleteChild As Boolean)
            Dim entry As ListEntryInfo = GetListEntryInfo(ListName, Value)
            DataProvider.Instance().DeleteListEntryByListName(ListName, Value, DeleteChild)
            ClearCache(entry.PortalID)
        End Sub

        Public Function GetListEntryInfo(ByVal EntryID As Integer) As ListEntryInfo ' Get single entry by ID
            Return CType(CBO.FillObject(DataProvider.Instance().GetListEntry(EntryID), GetType(ListEntryInfo)), ListEntryInfo)
        End Function

        Public Function GetListEntryInfo(ByVal ListName As String, ByVal Value As String) As ListEntryInfo ' Get single entry by ListName/Value
            Return CType(CBO.FillObject(DataProvider.Instance().GetListEntry(ListName, Value), GetType(ListEntryInfo)), ListEntryInfo)
        End Function

        Public Function GetListEntryInfoCollection(ByVal ListName As String) As ListEntryInfoCollection ' Get collection of entry lists
            Return GetListEntryInfoCollection(ListName, "")
        End Function

        Public Function GetListEntryInfoCollection(ByVal ListName As String, ByVal ParentKey As String) As ListEntryInfoCollection ' Get collection of entry lists
            Dim objListEntryInfoCollection As New ListEntryInfoCollection
            Dim arrListEntries As ArrayList = CBO.FillCollection(DataProvider.Instance().GetListEntriesByListName(ListName, ParentKey), GetType(ListEntryInfo))
            For Each entry As ListEntryInfo In arrListEntries
                objListEntryInfoCollection.Add(entry.Key, entry)
            Next
            Return objListEntryInfoCollection
        End Function

        Public Function GetListInfo(ByVal ListName As String) As ListInfo
            Return GetListInfo(ListName, "")
        End Function

        Public Function GetListInfo(ByVal ListName As String, ByVal ParentKey As String) As ListInfo
            Return GetListInfo(ListName, ParentKey, -1)
        End Function

        Public Function GetListInfo(ByVal ListName As String, ByVal ParentKey As String, ByVal PortalID As Integer) As ListInfo
            Dim list As ListInfo = Nothing
            Dim key As String = Null.NullString
            If Not String.IsNullOrEmpty(ParentKey) Then
                key = ParentKey + ":"
            End If
            key += ListName

            Dim dicLists As Dictionary(Of String, ListInfo) = GetListInfoDictionary(PortalID)

            If Not dicLists.TryGetValue(key, list) Then
                Dim dr As IDataReader = DataProvider.Instance().GetList(ListName, ParentKey, PortalID)
                Try
                    list = FillListInfo(dr, True)
                Finally
                    If Not dr Is Nothing Then
                        dr.Close()
                    End If
                End Try
            End If
            Return list
        End Function

        Public Function GetListInfoCollection() As ListInfoCollection
            Return GetListInfoCollection("")
        End Function

        Public Function GetListInfoCollection(ByVal ListName As String) As ListInfoCollection
            Return GetListInfoCollection(ListName, "")
        End Function

        Public Function GetListInfoCollection(ByVal ListName As String, ByVal ParentKey As String) As ListInfoCollection
            Return GetListInfoCollection(ListName, ParentKey, -1)
        End Function

        Public Function GetListInfoCollection(ByVal ListName As String, ByVal ParentKey As String, ByVal PortalID As Integer) As ListInfoCollection
            Dim lists As IList = New ListInfoCollection

            For Each listPair As KeyValuePair(Of String, ListInfo) In GetListInfoDictionary(PortalID)
                Dim list As ListInfo = listPair.Value
                If (list.Name = ListName OrElse String.IsNullOrEmpty(ListName)) AndAlso _
                            (list.ParentKey = ParentKey OrElse String.IsNullOrEmpty(ParentKey)) AndAlso _
                            (list.PortalID = PortalID OrElse PortalID = Null.NullInteger) Then
                    lists.Add(list)
                End If
            Next
            Return CType(lists, ListInfoCollection)
        End Function

        Public Sub UpdateListEntry(ByVal ListEntry As ListEntryInfo)
            DataProvider.Instance().UpdateListEntry(ListEntry.EntryID, ListEntry.Value, ListEntry.Text, ListEntry.Description)
            ClearCache(ListEntry.PortalID)
        End Sub

        Public Sub UpdateListSortOrder(ByVal EntryID As Integer, ByVal MoveUp As Boolean)
            DataProvider.Instance().UpdateListSortOrder(EntryID, MoveUp)

            Dim entry As ListEntryInfo = GetListEntryInfo(EntryID)
            ClearCache(entry.PortalID)
        End Sub

#End Region

        <Obsolete("This method has been deprecated. PLease use GetListEntryInfo(ByVal ListName As String, ByVal Value As String)")> _
        Public Function GetListEntryInfo(ByVal ListName As String, ByVal Value As String, ByVal ParentKey As String) As ListEntryInfo ' Get single entry by ListName/Value
            Return GetListEntryInfo(ListName, Value)
        End Function

        <Obsolete("This method has been deprecated. PLease use GetListEntryInfoCollection(ByVal ListName As String, ByVal ParentKey As String)")> _
        Public Function GetListEntryInfoCollection(ByVal ListName As String, ByVal Value As String, ByVal ParentKey As String) As ListEntryInfoCollection ' Get collection of entry lists
            Return GetListEntryInfoCollection(ListName, ParentKey)
        End Function

    End Class

End Namespace

