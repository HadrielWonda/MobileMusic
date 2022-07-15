using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using XamMusic.Interfaces;
using XamMusic.Models;
using Xamarin.Forms;
using XamMusic.Droid;
using Android.Provider;
using Android.Database;
using System.Collections.ObjectModel;

[assembly: Dependency(typeof(PlaylistManagerDroid))]
namespace XamMusic.Droid
{
    public class PlaylistManagerDroid : IPlaylistManager
    {
        private static string[] _mediaProjections =
        {
            MediaStore.Audio.Media.InterfaceConsts.Id,
            MediaStore.Audio.Media.InterfaceConsts.Artist,
            MediaStore.Audio.Media.InterfaceConsts.Album,
            MediaStore.Audio.Media.InterfaceConsts.Title,
            MediaStore.Audio.Media.InterfaceConsts.Duration,
            MediaStore.Audio.Media.InterfaceConsts.Data,
            MediaStore.Audio.Media.InterfaceConsts.IsMusic,
            MediaStore.Audio.Media.InterfaceConsts.AlbumId
        };

        private static string[] _genresProjections =
        {
            MediaStore.Audio.Genres.InterfaceConsts.Name,
            MediaStore.Audio.Genres.InterfaceConsts.Id
        };

        private static string[] _albumProjections =
        {
            MediaStore.Audio.Albums.InterfaceConsts.Id,
            MediaStore.Audio.Albums.InterfaceConsts.AlbumArt
        };

        private static string[] _playlistProjections =
        {
            MediaStore.Audio.Playlists.InterfaceConsts.Id,
            MediaStore.Audio.Playlists.InterfaceConsts.Name,
            MediaStore.Audio.Playlists.InterfaceConsts.DateModified
        };

        private static string[] _playlistSongsProjections =
                {
                    MediaStore.Audio.Playlists.Members.AudioId,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.Artist,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.Title,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.IsMusic,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.Album,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.Duration,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.Title,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.Data,
                    MediaStore.Audio.Playlists.Members.InterfaceConsts.AlbumId
                };

        public Playlist CreatePlaylist(string name)
        {
            ContentValues contentValues = new ContentValues();
            contentValues.Put(MediaStore.Audio.Playlists.InterfaceConsts.Name, name);
            contentValues.Put(MediaStore.Audio.Playlists.InterfaceConsts.DateAdded, Java.Lang.JavaSystem.CurrentTimeMillis());
            contentValues.Put(MediaStore.Audio.Playlists.InterfaceConsts.DateModified, Java.Lang.JavaSystem.CurrentTimeMillis());

            Android.Net.Uri uri = Android.App.Application.Context.ContentResolver.Insert(
                MediaStore.Audio.Playlists.ExternalContentUri, contentValues);
            if (uri != null)
            {
                ICursor playlistCursor = Android.App.Application.Context.ContentResolver.Query(uri, _playlistProjections, null, null, null);
                if (playlistCursor.MoveToFirst())
                {
                    ulong id = ulong.Parse(playlistCursor.GetString(playlistCursor.GetColumnIndex(MediaStore.Audio.Playlists.InterfaceConsts.Id)));
                    return new Playlist { Id = id, Title = name, Songs = new List<Song>(), IsDynamic = true, DateModified = DateTime.Now };
                }
                playlistCursor?.Close();
                
            }
            return null;
        }

        public async Task<IList<Song>> GetAllSongs()
        {
            return await Task.Run<IList<Song>>(() =>
            {
                IList<Song> songs = new ObservableCollection<Song>();
                ICursor mediaCursor, genreCursor, albumCursor;

                mediaCursor = Android.App.Application.Context.ContentResolver.Query(
                    MediaStore.Audio.Media.ExternalContentUri,
                    _mediaProjections, null, null,
                    MediaStore.Audio.Media.InterfaceConsts.TitleKey);

                int artistColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Artist);
                int albumColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Album);
                int titleColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Title);
                int durationColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Duration);
                int uriColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Data);
                int idColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Id);
                int isMusicColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.IsMusic);
                int albumIdColumn = mediaCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.AlbumId);

                int isMusic;
                ulong duration, id;
                string artist, album, title, uri, genre, artwork, artworkId;

                if (mediaCursor.MoveToFirst())
                {
                    do
                    {
                        isMusic = int.Parse(mediaCursor.GetString(isMusicColumn));
                        if (isMusic != 0)
                        {
                            artist = mediaCursor.GetString(artistColumn);
                            album = mediaCursor.GetString(albumColumn);
                            title = mediaCursor.GetString(titleColumn);
                            duration = ulong.Parse(mediaCursor.GetString(durationColumn));
                            uri = mediaCursor.GetString(uriColumn);
                            id = ulong.Parse(mediaCursor.GetString(idColumn));
                            artworkId = mediaCursor.GetString(albumIdColumn);

                            genreCursor = Android.App.Application.Context.ContentResolver.Query(
                                MediaStore.Audio.Genres.GetContentUriForAudioId("external", (int)id),
                                _genresProjections, null, null, null);
                            int genreColumn = genreCursor.GetColumnIndex(MediaStore.Audio.Genres.InterfaceConsts.Name);
                            if (genreCursor.MoveToFirst())
                            {
                                genre = genreCursor.GetString(genreColumn) ?? string.Empty;
                            }
                            else
                            {
                                genre = string.Empty;
                            }

                            albumCursor = Android.App.Application.Context.ContentResolver.Query(
                                MediaStore.Audio.Albums.ExternalContentUri,
                                _albumProjections,
                                $"{MediaStore.Audio.Albums.InterfaceConsts.Id}=?",
                                new string[] { artworkId },
                                null);
                            int artworkColumn = albumCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.AlbumArt);
                            if (albumCursor.MoveToFirst())
                            {
                                artwork = albumCursor.GetString(artworkColumn) ?? string.Empty;
                            }
                            else
                            {
                                artwork = string.Empty;
                            }

                            songs.Add(new Song
                            {
                                Id = id,
                                Title = title,
                                Artist = artist,
                                Album = album,
                                Genre = genre,
                                Duration = duration / 1000,
                                Uri = uri,
                                Artwork = artwork
                            });
                            genreCursor?.Close();
                            albumCursor?.Close();
                        }
                    } while (mediaCursor.MoveToNext());
                }
                mediaCursor?.Close();

                return songs;
            });
        }

        public IList<Playlist> GetPlaylists()
        {
            IList<Playlist> playlists = new ObservableCollection<Playlist>();

            ICursor playlistCursor = Android.App.Application.Context.ContentResolver.Query(
                MediaStore.Audio.Playlists.ExternalContentUri,
                _playlistProjections, null, null,
                MediaStore.Audio.Playlists.InterfaceConsts.Name);

            int idColumn = playlistCursor.GetColumnIndex(MediaStore.Audio.Playlists.InterfaceConsts.Id);
            int nameColumn = playlistCursor.GetColumnIndex(MediaStore.Audio.Playlists.InterfaceConsts.Name);
            int dateModifiedColumn = playlistCursor.GetColumnIndex(MediaStore.Audio.Playlists.InterfaceConsts.DateModified);

            ulong id;
            string name;
            long time;

            if (playlistCursor.MoveToFirst())
            {
                do
                {
                    id = ulong.Parse(playlistCursor.GetString(idColumn));
                    name = playlistCursor.GetString(nameColumn);
                    time = long.Parse(playlistCursor.GetString(dateModifiedColumn));

                    DateTime dateModified = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(time);

                    playlists.Add(new Playlist { Id = id, Title = name, IsDynamic = true , DateModified = dateModified});
                } while (playlistCursor.MoveToNext());
            }
            playlistCursor?.Close();
            return playlists;
        }

        public async Task<IList<Song>> GetPlaylistSongs(ulong playlistId)
        {
            return await Task.Run<IList<Song>>(() =>
            {
                IList<Song> songs = new ObservableCollection<Song>();
                ICursor playlistCursor, songCursor, genreCursor, albumCursor;


                playlistCursor = Android.App.Application.Context.ContentResolver.Query(
                    MediaStore.Audio.Playlists.ExternalContentUri,
                    _playlistProjections,
                    $"{MediaStore.Audio.Playlists.InterfaceConsts.Id} = {playlistId}",
                    null, null);

                if (playlistCursor.MoveToFirst())
                {
                    songCursor = Android.App.Application.Context.ContentResolver.Query(
                        MediaStore.Audio.Playlists.Members.GetContentUri("external", (long)playlistId),
                        _playlistSongsProjections,
                        $"{MediaStore.Audio.Playlists.Members.InterfaceConsts.IsMusic} != 0",
                        null,
                        MediaStore.Audio.Playlists.Members.PlayOrder);

                    int artistColumn = songCursor.GetColumnIndex(MediaStore.Audio.Playlists.Members.InterfaceConsts.Artist);
                    int titleColumn = songCursor.GetColumnIndex(MediaStore.Audio.Playlists.Members.InterfaceConsts.Title);
                    int albumColumn = songCursor.GetColumnIndex(MediaStore.Audio.Playlists.Members.InterfaceConsts.Album);
                    int uriColumn = songCursor.GetColumnIndex(MediaStore.Audio.Playlists.Members.InterfaceConsts.Data);
                    int durationColumn = songCursor.GetColumnIndex(MediaStore.Audio.Playlists.Members.InterfaceConsts.Duration);
                    int idColumn = songCursor.GetColumnIndex(MediaStore.Audio.Playlists.Members.AudioId);
                    int albumIdColumn = songCursor.GetColumnIndex(MediaStore.Audio.Playlists.Members.InterfaceConsts.AlbumId);


                    string artist, album, title, uri, artworkId, genre, artwork;
                    ulong duration, id;


                    if (songCursor.MoveToFirst())
                    {
                        do
                        {
                            artist = songCursor.GetString(artistColumn);
                            album = songCursor.GetString(albumColumn);
                            title = songCursor.GetString(titleColumn);
                            duration = ulong.Parse(songCursor.GetString(durationColumn));
                            uri = songCursor.GetString(uriColumn);
                            id = ulong.Parse(songCursor.GetString(idColumn));
                            artworkId = songCursor.GetString(albumIdColumn);

                            genreCursor = Android.App.Application.Context.ContentResolver.Query(
                                MediaStore.Audio.Genres.GetContentUriForAudioId("external", (int)id),
                                _genresProjections, null, null, null);
                            int genreColumn = genreCursor.GetColumnIndex(MediaStore.Audio.Genres.InterfaceConsts.Name);
                            if (genreCursor.MoveToFirst())
                            {
                                genre = genreCursor.GetString(genreColumn) ?? string.Empty;
                            }
                            else
                            {
                                genre = string.Empty;
                            }

                            albumCursor = Android.App.Application.Context.ContentResolver.Query(
                                MediaStore.Audio.Albums.ExternalContentUri,
                                _albumProjections,
                                $"{MediaStore.Audio.Albums.InterfaceConsts.Id}=?",
                                new string[] { artworkId },
                                null);
                            int artworkColumn = albumCursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.AlbumArt);
                            if (albumCursor.MoveToFirst())
                            {
                                artwork = albumCursor.GetString(artworkColumn) ?? string.Empty;
                            }
                            else
                            {
                                artwork = string.Empty;
                            }

                            songs.Add(new Song
                            {
                                Id = id,
                                Title = title,
                                Artist = artist,
                                Album = album,
                                Genre = genre,
                                Duration = duration / 1000,
                                Uri = uri,
                                Artwork = artwork
                            });

                            genreCursor?.Close();
                            albumCursor?.Close();
                        } while (songCursor.MoveToNext());
                        songCursor?.Close();
                    }
                }
                playlistCursor?.Close();

                return songs;
            });
            
        }

        public async Task AddToPlaylist(Playlist playlist, Song song)
        {
            await Task.Run(() =>
            {
                ContentValues cv = new ContentValues();
                cv.Put(MediaStore.Audio.Playlists.Members.PlayOrder, 0);
                cv.Put(MediaStore.Audio.Playlists.Members.AudioId, song.Id);
                Android.Net.Uri uri = MediaStore.Audio.Playlists.Members.GetContentUri("external", (long)playlist.Id);
                ContentResolver resolver = Android.App.Application.Context.ContentResolver;
                var rUri = resolver.Insert(uri, cv);
                resolver.NotifyChange(Android.Net.Uri.Parse("content://media"), null);
            });
            
        }
    }
}