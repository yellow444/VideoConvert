﻿//============================================================================
// VideoConvert - Fast Video & Audio Conversion Tool
// Copyright © 2012 JT-Soft
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//=============================================================================

using System.Collections.Generic;

namespace VideoConvert.Core.Helpers.TheMovieDB
{
    public static class MovieDBLanguages
    {
        public static List<MovieDBLanguage> LangList { get { return GenerateLangList(); } }

        private static List<MovieDBLanguage> GenerateLangList()
        {
            List<MovieDBLanguage> result = new List<MovieDBLanguage>
                {
                    new MovieDBLanguage {Code = "aa", Name = "Afar"},
                    new MovieDBLanguage {Code = "af", Name = "Afrikaans"},
                    new MovieDBLanguage {Code = "ak", Name = "Akana"},
                    new MovieDBLanguage {Code = "am", Name = "Amharic"},
                    new MovieDBLanguage {Code = "an", Name = "Aragones"},
                    new MovieDBLanguage {Code = "ar", Name = "Arabic"},
                    new MovieDBLanguage {Code = "as", Name = "Assamese"},
                    new MovieDBLanguage {Code = "ay", Name = "Aymara"},
                    new MovieDBLanguage {Code = "az", Name = "Azeri"},
                    new MovieDBLanguage {Code = "bar", Name = "Boarisch"},
                    new MovieDBLanguage {Code = "be", Name = "Belarusian"},
                    new MovieDBLanguage {Code = "bg", Name = "Bulgarian"},
                    new MovieDBLanguage {Code = "bh", Name = "Bihari"},
                    new MovieDBLanguage {Code = "bi", Name = "Bislama"},
                    new MovieDBLanguage {Code = "bm", Name = "Bambara"},
                    new MovieDBLanguage {Code = "bn", Name = "Bengali"},
                    new MovieDBLanguage {Code = "bo", Name = "Tibetan"},
                    new MovieDBLanguage {Code = "br", Name = "Breton"},
                    new MovieDBLanguage {Code = "bs", Name = "Bosnian"},
                    new MovieDBLanguage {Code = "ca", Name = "Catalan"},
                    new MovieDBLanguage {Code = "ce", Name = "Chechen"},
                    new MovieDBLanguage {Code = "ch", Name = "Chamorro"},
                    new MovieDBLanguage {Code = "co", Name = "Corsican"},
                    new MovieDBLanguage {Code = "cr", Name = "Cree"},
                    new MovieDBLanguage {Code = "cs", Name = "Czech"},
                    new MovieDBLanguage {Code = "cu", Name = "Old Slavonic"},
                    new MovieDBLanguage {Code = "cv", Name = "Chuvash"},
                    new MovieDBLanguage {Code = "cy", Name = "Welsh"},
                    new MovieDBLanguage {Code = "da", Name = "Danish"},
                    new MovieDBLanguage {Code = "de", Name = "Deutsch"},
                    new MovieDBLanguage {Code = "dv", Name = "Divehi"},
                    new MovieDBLanguage {Code = "dz", Name = "Dzongkha"},
                    new MovieDBLanguage {Code = "ee", Name = "Ewe"},
                    new MovieDBLanguage {Code = "el", Name = "Greek"},
                    new MovieDBLanguage {Code = "en", Name = "English"},
                    new MovieDBLanguage {Code = "eo", Name = "Esperanto"},
                    new MovieDBLanguage {Code = "es", Name = "Spanish"},
                    new MovieDBLanguage {Code = "et", Name = "Estonian"},
                    new MovieDBLanguage {Code = "eu", Name = "Basque"},
                    new MovieDBLanguage {Code = "fa", Name = "Persian"},
                    new MovieDBLanguage {Code = "ff", Name = "Fula"},
                    new MovieDBLanguage {Code = "fi", Name = "Finnish"},
                    new MovieDBLanguage {Code = "fj", Name = "Fijian"},
                    new MovieDBLanguage {Code = "fo", Name = "Faroese"},
                    new MovieDBLanguage {Code = "fr", Name = "French"},
                    new MovieDBLanguage {Code = "fy", Name = "Western Frisian"},
                    new MovieDBLanguage {Code = "ga", Name = "Irish"},
                    new MovieDBLanguage {Code = "gd", Name = "(Scottish) Gaelic"},
                    new MovieDBLanguage {Code = "gl", Name = "Galician"},
                    new MovieDBLanguage {Code = "gn", Name = "Guarani"},
                    new MovieDBLanguage {Code = "gu", Name = "Gujarati"},
                    new MovieDBLanguage {Code = "gv", Name = "Gaelg"},
                    new MovieDBLanguage {Code = "ha", Name = "Hausa"},
                    new MovieDBLanguage {Code = "he", Name = "Hebrew"},
                    new MovieDBLanguage {Code = "hi", Name = "Hindi"},
                    new MovieDBLanguage {Code = "ho", Name = "Hiri Motu"},
                    new MovieDBLanguage {Code = "hr", Name = "Croatian"},
                    new MovieDBLanguage {Code = "ht", Name = "Haitian"},
                    new MovieDBLanguage {Code = "hu", Name = "Hungarian"},
                    new MovieDBLanguage {Code = "hy", Name = "Armenian"},
                    new MovieDBLanguage {Code = "hz", Name = "Herero"},
                    new MovieDBLanguage {Code = "ia", Name = "Interlingua"},
                    new MovieDBLanguage {Code = "id", Name = "Indonesian"},
                    new MovieDBLanguage {Code = "ie", Name = "Interlingue"},
                    new MovieDBLanguage {Code = "ig", Name = "Igbo"},
                    new MovieDBLanguage {Code = "ii", Name = "Nuosu"},
                    new MovieDBLanguage {Code = "ik", Name = "Inupiaq"},
                    new MovieDBLanguage {Code = "io", Name = "Ido"},
                    new MovieDBLanguage {Code = "is", Name = "Icelandic"},
                    new MovieDBLanguage {Code = "it", Name = "Italian"},
                    new MovieDBLanguage {Code = "iu", Name = "Inuktitut"},
                    new MovieDBLanguage {Code = "ja", Name = "Japanese"},
                    new MovieDBLanguage {Code = "jv", Name = "Javanese"},
                    new MovieDBLanguage {Code = "ka", Name = "Georgian"},
                    new MovieDBLanguage {Code = "kg", Name = "Kongo"},
                    new MovieDBLanguage {Code = "ki", Name = "Kikuyu"},
                    new MovieDBLanguage {Code = "kj", Name = "Kwanyama"},
                    new MovieDBLanguage {Code = "kk", Name = "Kazakh"},
                    new MovieDBLanguage {Code = "kl", Name = "Kalaallisut"},
                    new MovieDBLanguage {Code = "km", Name = "Khmer"},
                    new MovieDBLanguage {Code = "kn", Name = "Kannada"},
                    new MovieDBLanguage {Code = "ko", Name = "Korean"},
                    new MovieDBLanguage {Code = "kr", Name = "Kanuri"},
                    new MovieDBLanguage {Code = "ks", Name = "Kashmiri"},
                    new MovieDBLanguage {Code = "ku", Name = "Kurdish"},
                    new MovieDBLanguage {Code = "kv", Name = "Komi"},
                    new MovieDBLanguage {Code = "kw", Name = "Cornish"},
                    new MovieDBLanguage {Code = "ky", Name = "Kyrgyz"},
                    new MovieDBLanguage {Code = "la", Name = "Latin"},
                    new MovieDBLanguage {Code = "lb", Name = "Lëtzebuergesch"},
                    new MovieDBLanguage {Code = "lg", Name = "Ganda"},
                    new MovieDBLanguage {Code = "li", Name = "Limburgish"},
                    new MovieDBLanguage {Code = "ln", Name = "Lingala"},
                    new MovieDBLanguage {Code = "lo", Name = "Lao"},
                    new MovieDBLanguage {Code = "lt", Name = "Lithuanian"},
                    new MovieDBLanguage {Code = "lv", Name = "Latvian"},
                    new MovieDBLanguage {Code = "mg", Name = "Malagasy"},
                    new MovieDBLanguage {Code = "mh", Name = "Marshallese"},
                    new MovieDBLanguage {Code = "mi", Name = "Maori"},
                    new MovieDBLanguage {Code = "mk", Name = "Macedonian"},
                    new MovieDBLanguage {Code = "ml", Name = "Malayalam"},
                    new MovieDBLanguage {Code = "mn", Name = "Mongolian"},
                    new MovieDBLanguage {Code = "mr", Name = "Marathi"},
                    new MovieDBLanguage {Code = "ms", Name = "Bahasa Melayu"},
                    new MovieDBLanguage {Code = "mt", Name = "Maltese"},
                    new MovieDBLanguage {Code = "my", Name = "Burmese"},
                    new MovieDBLanguage {Code = "na", Name = "Nauru"},
                    new MovieDBLanguage {Code = "nd", Name = "North Ndebele"},
                    new MovieDBLanguage {Code = "ne", Name = "Nepali"},
                    new MovieDBLanguage {Code = "ng", Name = "Ndonga"},
                    new MovieDBLanguage {Code = "nl", Name = "Nederlands"},
                    new MovieDBLanguage {Code = "nn", Name = "Norwegian (nynorsk)"},
                    new MovieDBLanguage {Code = "no", Name = "Norwegian"},
                    new MovieDBLanguage {Code = "nr", Name = "South Ndebele"},
                    new MovieDBLanguage {Code = "oc", Name = "Occitan"},
                    new MovieDBLanguage {Code = "oj", Name = "Ojibwe"},
                    new MovieDBLanguage {Code = "om", Name = "Oromo"},
                    new MovieDBLanguage {Code = "or", Name = "Oriya"},
                    new MovieDBLanguage {Code = "os", Name = "Ossetian"},
                    new MovieDBLanguage {Code = "pa", Name = "Panjabi / Punjabi"},
                    new MovieDBLanguage {Code = "pi", Name = "Pali"},
                    new MovieDBLanguage {Code = "pl", Name = "Polish"},
                    new MovieDBLanguage {Code = "ps", Name = "Pashto"},
                    new MovieDBLanguage {Code = "pt", Name = "Portugues"},
                    new MovieDBLanguage {Code = "qu", Name = "Quechua"},
                    new MovieDBLanguage {Code = "rm", Name = "Romansh"},
                    new MovieDBLanguage {Code = "rn", Name = "Kirundi"},
                    new MovieDBLanguage {Code = "ro", Name = "Romanian"},
                    new MovieDBLanguage {Code = "ru", Name = "Russian"},
                    new MovieDBLanguage {Code = "sa", Name = "Sanskrit"},
                    new MovieDBLanguage {Code = "sc", Name = "Sardinian"},
                    new MovieDBLanguage {Code = "sd", Name = "Sindhi"},
                    new MovieDBLanguage {Code = "se", Name = "Northern Sami"},
                    new MovieDBLanguage {Code = "sg", Name = "Sango"},
                    new MovieDBLanguage {Code = "si", Name = "Sinhala"},
                    new MovieDBLanguage {Code = "sk", Name = "Slovak"},
                    new MovieDBLanguage {Code = "sl", Name = "Slovene"},
                    new MovieDBLanguage {Code = "sm", Name = "Samoan"},
                    new MovieDBLanguage {Code = "sn", Name = "Shona"},
                    new MovieDBLanguage {Code = "so", Name = "Somali"},
                    new MovieDBLanguage {Code = "sq", Name = "Albanian"},
                    new MovieDBLanguage {Code = "sr", Name = "Serbian"},
                    new MovieDBLanguage {Code = "ss", Name = "Swati"},
                    new MovieDBLanguage {Code = "st", Name = "Southern Sotho"},
                    new MovieDBLanguage {Code = "su", Name = "Sundanese"},
                    new MovieDBLanguage {Code = "sv", Name = "Swedish"},
                    new MovieDBLanguage {Code = "sw", Name = "Swahili"},
                    new MovieDBLanguage {Code = "ta", Name = "Tamil"},
                    new MovieDBLanguage {Code = "te", Name = "Telugu"},
                    new MovieDBLanguage {Code = "tg", Name = "Tajik"},
                    new MovieDBLanguage {Code = "th", Name = "Thai"},
                    new MovieDBLanguage {Code = "ti", Name = "Tigrinya"},
                    new MovieDBLanguage {Code = "tk", Name = "Turkmen"},
                    new MovieDBLanguage {Code = "tl", Name = "Tagalog"},
                    new MovieDBLanguage {Code = "tn", Name = "Tswana"},
                    new MovieDBLanguage {Code = "to", Name = "Tonga"},
                    new MovieDBLanguage {Code = "tr", Name = "Turkish"},
                    new MovieDBLanguage {Code = "ts", Name = "Tsonga"},
                    new MovieDBLanguage {Code = "tt", Name = "Tatar"},
                    new MovieDBLanguage {Code = "tw", Name = "Twi"},
                    new MovieDBLanguage {Code = "ty", Name = "Tahitian"},
                    new MovieDBLanguage {Code = "ug", Name = "Uighur"},
                    new MovieDBLanguage {Code = "uk", Name = "Ukrainian"},
                    new MovieDBLanguage {Code = "ur", Name = "Urdu"},
                    new MovieDBLanguage {Code = "uz", Name = "Uzbek"},
                    new MovieDBLanguage {Code = "ve", Name = "Venda"},
                    new MovieDBLanguage {Code = "vi", Name = "Vietnamese"},
                    new MovieDBLanguage {Code = "vo", Name = "Volapük"},
                    new MovieDBLanguage {Code = "wa", Name = "Walloon"},
                    new MovieDBLanguage {Code = "wo", Name = "Wolof"},
                    new MovieDBLanguage {Code = "xh", Name = "Xhosa"},
                    new MovieDBLanguage {Code = "yi", Name = "Yiddish"},
                    new MovieDBLanguage {Code = "yo", Name = "Yoruba"},
                    new MovieDBLanguage {Code = "za", Name = "Zhuang"},
                    new MovieDBLanguage {Code = "zh", Name = "Chinese"},
                    new MovieDBLanguage {Code = "zu", Name = "Zulu"}
                };

            return result;
        }
    }
}
