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


Imports DotNetNuke.Common.Utilities
Imports DotNetNuke.Framework
Imports System.Text.RegularExpressions
Imports System.IO
Imports System.Web
Imports DotNetNuke.Framework.Providers
Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Security
Imports DotNetNuke.Services.Exceptions
Imports DotNetNuke.Services.Log.EventLog

Namespace DotNetNuke.Services.Search

    ''' -----------------------------------------------------------------------------
    ''' Namespace:  DotNetNuke.Services.Search
    ''' Project:    DotNetNuke.Search.DataStore
    ''' Class:      SearchDataStore
    ''' -----------------------------------------------------------------------------
    ''' <summary>
    ''' The SearchDataStore is an implementation of the abstract SearchDataStoreProvider
    ''' class
    ''' </summary>
    ''' <remarks>
    ''' </remarks>
    ''' <history>
    '''		[cnurse]	11/15/2004	documented
    ''' </history>
    ''' -----------------------------------------------------------------------------
    Public Class SearchDataStore
        Inherits SearchDataStoreProvider

#Region "Private Members"

        Private _settings As Hashtable
        Private _defaultSettings As Hashtable
        Private maxWordLength As Integer = 50
        Private minWordLength As Integer = 4
        Private includeNumbers As Boolean = True
        Private includeCommon As Boolean = False

#End Region

#Region "Private Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' AddIndexWords adds the Index Words to the Data Store
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="IndexId">The Id of the SearchItem</param>
        ''' <param name="SearchItem">The SearchItem</param>
        ''' <param name="Language">The Language of the current Item</param>
        ''' <history>
        '''		[cnurse]	11/15/2004	documented
        '''     [cnurse]    11/16/2004  replaced calls to separate content clean-up
        '''                             functions with new call to HtmlUtils.Clean().
        '''                             replaced logic to determine whether word should
        '''                             be indexed by call to CanIndexWord()
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Sub AddIndexWords(ByVal IndexId As Integer, ByVal SearchItem As SearchItemInfo, ByVal Language As String)

            Dim IndexWords As New Hashtable
            Dim IndexPositions As New Hashtable
            Dim setting As String

            'Get the Settings for this Module(Portal)
            _settings = SearchDataStoreController.GetSearchSettings(SearchItem.ModuleId)
            If _settings Is Nothing Then
                'Try Host Settings
                _settings = Common.Globals.HostSettings
            End If
            setting = GetSetting("MaxSearchWordLength")
            If setting <> "" Then
                maxWordLength = Integer.Parse(setting)
            End If
            setting = GetSetting("MinSearchWordLength")
            If setting <> "" Then
                minWordLength = Integer.Parse(setting)
            End If
            setting = GetSetting("SearchIncludeCommon")
            If setting = "Y" Then
                includeCommon = True
            End If
            setting = GetSetting("SearchIncludeNumeric")
            If setting = "N" Then
                includeNumbers = False
            End If

            Dim Content As String = SearchItem.Content

            ' clean content
            Content = HtmlUtils.Clean(Content, True)
            Content = Content.ToLower

            '' split content into words
            Dim ContentWords() As String = Split(Content, " ")

            ' process each word
            Dim intWord As Integer
            Dim strWord As String
            For Each strWord In ContentWords
                If CanIndexWord(strWord, Language) Then
                    intWord = intWord + 1
                    If IndexWords.ContainsKey(strWord) = False Then
                        IndexWords.Add(strWord, 0)
                        IndexPositions.Add(strWord, 1)
                    End If
                    ' track number of occurrences of word in content
                    IndexWords(strWord) = CType(IndexWords(strWord), Integer) + 1
                    ' track positions of word in content
                    IndexPositions(strWord) = CType(IndexPositions(strWord), String) & "," & intWord.ToString
                End If
            Next

            ' get list of words ( non-common )
            Dim Words As Hashtable = GetSearchWords()    ' this could be cached
            Dim WordId As Integer

            '' iterate through each indexed word
            Dim objWord As Object
            For Each objWord In IndexWords.Keys
                strWord = CType(objWord, String)
                If Words.ContainsKey(strWord) Then
                    ' word is in the DataStore
                    WordId = CType(Words(strWord), Integer)
                Else
                    ' add the word to the DataStore
                    WordId = Data.DataProvider.Instance().AddSearchWord(strWord)
                    Words.Add(strWord, WordId)
                End If
                ' add the indexword
                Dim SearchItemWordID As Integer = Data.DataProvider.Instance().AddSearchItemWord(IndexId, WordId, CType(IndexWords(strWord), Integer))
                Data.DataProvider.Instance().AddSearchItemWordPosition(SearchItemWordID, CType(IndexPositions(strWord), String))
            Next

        End Sub

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' CanIndexWord determines whether the Word should be indexed
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="strWord">The Word to validate</param>
        ''' <returns>True if indexable, otherwise false</returns>
        ''' <history>
        '''		[cnurse]	11/16/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function CanIndexWord(ByVal strWord As String, ByVal Locale As String) As Boolean

            'Create Boolean to hold return value
            Dim retValue As Boolean = True

            ' get common words for exclusion
            Dim CommonWords As Hashtable = GetCommonWords(Locale)

            'Determine if Word is actually a number
            If IsNumeric(strWord) Then
                'Word is Numeric
                If Not includeNumbers Then
                    retValue = False
                End If
            Else
                'Word is Non-Numeric

                'Determine if Word satisfies Minimum/Maximum length
                If strWord.Length < minWordLength Or strWord.Length > maxWordLength Then
                    retValue = False
                Else
                    'Determine if Word is a Common Word (and should be excluded)
                    If CommonWords.ContainsKey(strWord) = True And Not includeCommon Then
                        retValue = False
                    End If
                End If
            End If


            Return retValue

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetCommonWords gets a list of the Common Words for the locale
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="Locale">The locale string</param>
        ''' <returns>A hashtable of common words</returns>
        ''' <history>
        '''		[cnurse]	11/15/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function GetCommonWords(ByVal Locale As String) As Hashtable
            Dim strCacheKey As String = "CommonWords" & Locale

            Dim objWords As Hashtable = CType(DataCache.GetCache(strCacheKey), Hashtable)
            If objWords Is Nothing Then
                objWords = New Hashtable
                Dim drWords As IDataReader = Data.DataProvider.Instance().GetSearchCommonWordsByLocale(Locale)
                Try
                    While drWords.Read
                        objWords.Add(drWords("CommonWord").ToString, drWords("CommonWord").ToString)
                    End While
                Finally
                    drWords.Close()
                    drWords.Dispose()
                End Try
                DataCache.SetCache(strCacheKey, objWords)
            End If
            Return objWords
        End Function

        Private Overloads Function GetSearchItems(ByVal ModuleId As Integer) As SearchItemInfoCollection

            Return New SearchItemInfoCollection(CBO.FillCollection(Data.DataProvider.Instance().GetSearchItems(Null.NullInteger, Null.NullInteger, ModuleId), GetType(SearchItemInfo)))

        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetSearchWords gets a list of the current Words in the Database's Index
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <returns>A hashtable of words</returns>
        ''' <history>
        '''		[cnurse]	11/15/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function GetSearchWords() As Hashtable
            Dim strCacheKey As String = "SearchWords"

            Dim objWords As Hashtable = CType(DataCache.GetCache(strCacheKey), Hashtable)
            If objWords Is Nothing Then
                objWords = New Hashtable
                Dim drWords As IDataReader = Data.DataProvider.Instance().GetSearchWords()
                Try
                    While drWords.Read
                        objWords.Add(drWords("Word").ToString, drWords("SearchWordsID"))
                    End While
                Finally
                    drWords.Close()
                    drWords.Dispose()
                End Try
                DataCache.SetCache(strCacheKey, objWords, TimeSpan.FromMinutes(2))
            End If
            Return objWords
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetSetting gets a Search Setting from the Portal Modules Settings table (or
        ''' from the Host Settings)
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>
        ''' </remarks>
        ''' <history>
        ''' 	[cnurse]	11/16/2004	created
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Private Function GetSetting(ByVal txtName As String) As String

            Dim settingValue As String = ""

            'Try Portal setting first
            If _settings(txtName) Is Nothing = False Then
                settingValue = CType(_settings(txtName), String)
            Else
                'Get Default setting
                If _defaultSettings(txtName) Is Nothing = False Then
                    settingValue = CType(_defaultSettings(txtName), String)
                End If
            End If

            Return settingValue

        End Function

#End Region

#Region "Public Methods"

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetSearchItems gets a collection of Search Items for a Module/Tab/Portal
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="PortalID">A Id of the Portal</param>
        ''' <param name="TabID">A Id of the Tab</param>
        ''' <param name="ModuleID">A Id of the Module</param>
        ''' <history>
        '''		[cnurse]	11/15/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overloads Overrides Function GetSearchItems(ByVal PortalID As Integer, ByVal TabID As Integer, ByVal ModuleID As Integer) As SearchResultsInfoCollection
            Return New SearchResultsInfoCollection(CBO.FillCollection(Data.DataProvider.Instance().GetSearchItems(PortalID, TabID, ModuleID), GetType(SearchResultsInfo)))
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' GetSearchResults gets the search results for a passed in criteria string
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="PortalID">A Id of the Portal</param>
        ''' <param name="Criteria">The criteria string</param>
        ''' <history>
        '''		[cnurse]	11/15/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Function GetSearchResults(ByVal PortalID As Integer, ByVal Criteria As String) As SearchResultsInfoCollection

            'We will assume that the content is in the locale of the Portal
            Dim objPortalController As New PortalController
            Dim objPortal As PortalInfo = objPortalController.GetPortal(PortalID)
            Dim locale As String = objPortal.DefaultLanguage
            Dim CommonWords As Hashtable = GetCommonWords(locale)
            Dim setting As String

            'Get the default Search Settings
            _defaultSettings = Common.Globals.HostSettings

            'Get the Settings for this Portal
            Dim objModuleController As New ModuleController
            Dim objModule As ModuleInfo = objModuleController.GetModuleByDefinition(-1, "Search Admin")
            If Not objModule Is Nothing Then
                _settings = Entities.Portals.PortalSettings.GetModuleSettings(objModule.ModuleID)
            End If
            setting = GetSetting("SearchIncludeCommon")
            If setting = "Y" Then
                includeCommon = True
            End If

            ' clean criteria
            Criteria = Criteria.ToLower

            ' split search criteria into words
            Dim SearchWords As New SearchCriteriaCollection(Criteria)
            Dim SearchResults As New Hashtable

            ' iterate through search criteria words
            Dim Criterion As SearchCriteria
            For Each Criterion In SearchWords
                If CommonWords.ContainsKey(Criterion.Criteria) = False OrElse includeCommon Then
                    Dim ResultsCollection As SearchResultsInfoCollection = SearchDataStoreController.GetSearchResults(PortalID, Criterion.Criteria)
                    If Criterion.MustExclude = False Then
                        ' Add all these to the results
                        For Each Result As SearchResultsInfo In ResultsCollection
                            If SearchResults.ContainsKey(Result.SearchItemID) Then
                                CType(SearchResults.Item(Result.SearchItemID), SearchResultsInfo).Relevance += Result.Relevance
                            Else
                                SearchResults.Add(Result.SearchItemID, Result)
                            End If
                        Next
                    End If
                End If
            Next

            ' Validate MustInclude and MustExclude
            For Each Criterion In SearchWords
                Dim ResultsCollection As SearchResultsInfoCollection = SearchDataStoreController.GetSearchResults(PortalID, Criterion.Criteria)
                If Criterion.MustInclude Then
                    ' We need to remove items which do not include this term
                    Dim MandatoryResults As New Hashtable
                    For Each Result As SearchResultsInfo In ResultsCollection
                        MandatoryResults.Add(result.SearchItemID, 0)
                    Next
                    For Each Result As SearchResultsInfo In SearchResults.Values
                        If MandatoryResults.ContainsKey(result.SearchItemID) = False Then
                            Result.Delete = True
                        End If
                    Next
                End If
                If Criterion.MustExclude Then
                    ' We need to remove items which do include this term
                    Dim ExcludedResults As New Hashtable
                    For Each Result As SearchResultsInfo In ResultsCollection
                        ExcludedResults.Add(result.SearchItemID, 0)
                    Next
                    For Each Result As SearchResultsInfo In SearchResults.Values
                        If ExcludedResults.ContainsKey(Result.SearchItemID) = True Then
                            Result.Delete = True
                        End If
                    Next
                End If
            Next

            'Only include results we have permission to see
            Dim Results As New SearchResultsInfoCollection
            Dim objTabController As New TabController
            Dim hashTabsAllowed As New Hashtable
            For Each SearchResult As SearchResultsInfo In SearchResults.Values
                If Not SearchResult.Delete Then
                    'Check If authorised to View Tab
                    Dim hashModulesAllowed As Hashtable
                    Dim tabAllowed As Object = hashTabsAllowed(SearchResult.TabId)
                    If tabAllowed Is Nothing Then
                        Dim objTab As TabInfo = objTabController.GetTab(SearchResult.TabId, PortalID, False)
                        If PortalSecurity.IsInRoles(objTab.AuthorizedRoles) Then
                            hashModulesAllowed = New Hashtable
                            tabAllowed = hashModulesAllowed
                        Else
                            tabAllowed = 0
                            hashModulesAllowed = Nothing
                        End If
                        hashTabsAllowed.Add(SearchResult.TabId, tabAllowed)
                    Else
                        If TypeOf tabAllowed Is Hashtable Then
                            hashModulesAllowed = CType(tabAllowed, Hashtable)
                        Else
                            hashModulesAllowed = Nothing
                        End If
                    End If

                    If Not hashModulesAllowed Is Nothing Then
                        Dim addResult As Boolean
                        If Not hashModulesAllowed.ContainsKey(SearchResult.ModuleId) Then
                            'Now check if authorized to view module
                            objModule = objModuleController.GetModule(SearchResult.ModuleId, SearchResult.TabId, False)
                            addResult = (objModule.IsDeleted = False AndAlso PortalSecurity.IsInRoles(objModule.AuthorizedViewRoles))
                            hashModulesAllowed.Add(SearchResult.ModuleId, addResult)
                        Else
                            addResult = CType(hashModulesAllowed(SearchResult.ModuleId), Boolean)
                        End If

                        If addResult Then Results.Add(SearchResult)
                    End If
                End If
            Next

            'Return Search Results Collection
            Return Results
        End Function

        ''' -----------------------------------------------------------------------------
        ''' <summary>
        ''' StoreSearchItems adds the Search Item to the Data Store
        ''' </summary>
        ''' <remarks>
        ''' </remarks>
        ''' <param name="SearchItems">A Collection of SearchItems</param>
        ''' <history>
        '''		[cnurse]	11/15/2004	documented
        ''' </history>
        ''' -----------------------------------------------------------------------------
        Public Overrides Sub StoreSearchItems(ByVal SearchItems As SearchItemInfoCollection)

            Dim i As Integer

            'Get the default Search Settings
            _defaultSettings = Common.Globals.HostSettings


            'For now as we don't support Localized content - set the locale to the default locale. This
            'is to avoid the error in GetDefaultLanguageByModule which artificially limits the number
            'of modules that can be indexed.  This will need to be addressed when we support localized content.
            Dim Modules As New Hashtable
            For i = 0 To SearchItems.Count - 1
                If Not Modules.ContainsKey(SearchItems(i).ModuleId.ToString) Then
                    Modules.Add(SearchItems(i).ModuleId.ToString, "en-US")
                End If
            Next

            Dim SearchItem As SearchItemInfo
            Dim IndexedItem As SearchItemInfo
            Dim IndexedItems As SearchItemInfoCollection
            Dim ModuleItems As SearchItemInfoCollection
            Dim IndexID As Integer
            Dim iSearch As Integer
            Dim ModuleId As Integer
            Dim Language As String
            Dim ItemFound As Boolean

            'Process the SearchItems by Module to reduce Database hits
            Dim moduleEnumerator As IDictionaryEnumerator = Modules.GetEnumerator()
            While moduleEnumerator.MoveNext()
                ModuleId = CType(moduleEnumerator.Key, Integer)
                Language = CType(moduleEnumerator.Value, String)

                'Get the Indexed Items that are in the Database for this Module
                IndexedItems = GetSearchItems(ModuleId)
                'Get the Module's SearchItems to compare
                ModuleItems = SearchItems.ModuleItems(ModuleId)

                'As we will be potentially removing items from the collection iterate backwards
                For iSearch = ModuleItems.Count - 1 To 0 Step -1
                    SearchItem = ModuleItems(iSearch)
                    ItemFound = False

                    'Iterate through Indexed Items
                    For Each IndexedItem In IndexedItems
                        'Compare the SearchKeys
                        If SearchItem.SearchKey = IndexedItem.SearchKey Then
                            'Item exists so compare Dates to see if modified
                            If IndexedItem.PubDate < SearchItem.PubDate Then
                                Try
                                    'Content modified so update SearchItem and delete item's Words Collection
                                    SearchItem.SearchItemId = IndexedItem.SearchItemId
                                    SearchDataStoreController.UpdateSearchItem(SearchItem)
                                    SearchDataStoreController.DeleteSearchItemWords(SearchItem.SearchItemId)

                                    ' re-index the content
                                    AddIndexWords(SearchItem.SearchItemId, SearchItem, Language)
                                Catch ex As Exception
                                    'Log Exception
                                    LogException(ex)
                                End Try
                            End If

                            'Remove Items from both collections
                            IndexedItems.Remove(IndexedItem)
                            ModuleItems.Remove(SearchItem)

                            'Exit the Iteration as Match found
                            ItemFound = True
                            Exit For
                        End If
                    Next

                    If Not ItemFound Then
                        Try
                            'Item doesn't exist so Add to Index
                            IndexID = SearchDataStoreController.AddSearchItem(SearchItem)
                            ' index the content
                            AddIndexWords(IndexID, SearchItem, Language)
                        Catch ex As Exception
                            'Log Exception
                            'LogException(ex) ** this exception has been suppressed because it fills up the event log with duplicate key errors - we still need to understand what causes it though
                        End Try
                    End If

                Next

                'As we removed the IndexedItems as we matched them the remaining items are deleted Items
                'ie they have been indexed but are no longer present
                Dim ht As New Hashtable
                For Each IndexedItem In IndexedItems
                    Try
                        'dedupe
                        If ht(IndexedItem.SearchItemId) Is Nothing Then
                            SearchDataStoreController.DeleteSearchItem(IndexedItem.SearchItemId)
                            ht.Add(IndexedItem.SearchItemId, 0)
                        End If
                    Catch ex As Exception
                        'Log Exception
                        LogException(ex)
                    End Try
                Next

            End While

        End Sub

#End Region

    End Class
End Namespace