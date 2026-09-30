import 'package:flutter/material.dart';

class CeylonColors {
  const CeylonColors._();
  static const forest = Color(0xFF0E3B2E);
  static const forestDeep = Color(0xFF00241A);
  static const tea = Color(0xFF2D6A4F);
  static const teal = Color(0xFF028090);
  static const mint = Color(0xFFD8F3DC);
  static const ivory = Color(0xFFFDFBF7);
  static const canvas = Color(0xFFF6F3ED);
  static const surfaceSoft = Color(0xFFF0EEE7);
  static const ink = Color(0xFF171C25);
  static const inkMuted = Color(0xFF59645F);
  static const outline = Color(0xFFC0C8C3);
  static const amber = Color(0xFFD4A373);
  static const error = Color(0xFFBA1A1A);
}

class CeylonSpacing {
  const CeylonSpacing._();
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 16.0;
  static const lg = 24.0;
  static const xl = 32.0;
}

class CeylonRadii {
  const CeylonRadii._();
  static const card = 22.0;
  static const field = 14.0;
  static const pill = 999.0;
}

class AppTheme {
  const AppTheme._();

  static ThemeData get light {
    final colorScheme =
        ColorScheme.fromSeed(
          seedColor: CeylonColors.forest,
          brightness: Brightness.light,
        ).copyWith(
          primary: CeylonColors.forest,
          onPrimary: Colors.white,
          secondary: CeylonColors.tea,
          onSecondary: Colors.white,
          surface: CeylonColors.ivory,
          onSurface: CeylonColors.ink,
          error: CeylonColors.error,
        );

    return ThemeData(
      useMaterial3: true,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: CeylonColors.canvas,
      fontFamily: 'Plus Jakarta Sans',
      fontFamilyFallback: const ['Arial', 'sans-serif'],
      textTheme: const TextTheme(
        displaySmall: TextStyle(
          fontFamily: 'Playfair Display',
          fontFamilyFallback: ['Georgia', 'serif'],
          fontWeight: FontWeight.w600,
          color: CeylonColors.forestDeep,
          letterSpacing: -0.8,
        ),
        headlineMedium: TextStyle(
          fontFamily: 'Playfair Display',
          fontFamilyFallback: ['Georgia', 'serif'],
          fontWeight: FontWeight.w600,
          color: CeylonColors.forestDeep,
        ),
        titleLarge: TextStyle(
          fontWeight: FontWeight.w700,
          color: CeylonColors.forest,
        ),
        titleMedium: TextStyle(
          fontWeight: FontWeight.w700,
          color: CeylonColors.forest,
        ),
        bodyLarge: TextStyle(height: 1.55, color: CeylonColors.inkMuted),
        bodyMedium: TextStyle(height: 1.45, color: CeylonColors.inkMuted),
        labelLarge: TextStyle(fontWeight: FontWeight.w800, letterSpacing: 0.1),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: Colors.transparent,
        foregroundColor: CeylonColors.forest,
        elevation: 0,
        centerTitle: false,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: CeylonColors.surfaceSoft,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 15,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(CeylonRadii.field),
          borderSide: const BorderSide(color: CeylonColors.outline),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(CeylonRadii.field),
          borderSide: const BorderSide(color: Color(0xFFE0E4E1)),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(CeylonRadii.field),
          borderSide: const BorderSide(color: CeylonColors.teal, width: 1.5),
        ),
        labelStyle: const TextStyle(color: CeylonColors.inkMuted),
        hintStyle: const TextStyle(color: Color(0xFF7E8782)),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          backgroundColor: CeylonColors.forest,
          foregroundColor: Colors.white,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(CeylonRadii.field),
          ),
          textStyle: const TextStyle(fontWeight: FontWeight.w800),
        ),
      ),
      cardTheme: CardThemeData(
        color: CeylonColors.ivory,
        elevation: 0,
        surfaceTintColor: Colors.transparent,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(CeylonRadii.card),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: CeylonColors.ivory,
        elevation: 8,
        indicatorColor: CeylonColors.mint,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => TextStyle(
            color: states.contains(WidgetState.selected)
                ? CeylonColors.forest
                : CeylonColors.inkMuted,
            fontSize: 11,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
    );
  }
}
