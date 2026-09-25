package com.squishydumpling.widget;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.widget.RemoteViews;

/**
 * Home-screen widget: the squishy's picture, name and mood. The game saves the needs, their drain rates and
 * pictures for each mood; the widget works out the current mood itself every 30 minutes, so it stays live
 * while the app is closed.
 */
public class SquishyWidget extends AppWidgetProvider {
    public static final String PREFS = "squishy_widget";

    @Override
    public void onUpdate(Context c, AppWidgetManager m, int[] ids) {
        for (int id : ids) update(c, m, id);
    }

    /** Called from Unity after it writes the preferences. */
    public static void refresh(Context c) {
        AppWidgetManager m = AppWidgetManager.getInstance(c);
        int[] ids = m.getAppWidgetIds(new ComponentName(c, SquishyWidget.class));
        for (int id : ids) update(c, m, id);
    }

    static void update(Context c, AppWidgetManager m, int id) {
        SharedPreferences p = c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        String name = p.getString("name", "Squishy");
        String mood;
        String img;
        if (p.getBoolean("dead", false)) { mood = "Resting in peace"; img = "sad"; }
        else if (p.getBoolean("tucked", false)) { mood = "Tucked in, fast asleep"; img = "happy"; }
        else {
            float secs = (System.currentTimeMillis() - p.getLong("saved", System.currentTimeMillis())) / 1000f;
            float min = 1f;
            for (int k = 0; k < 4; k++) min = Math.min(min, p.getFloat("need" + k, 1f) - p.getFloat("rate" + k, 0f) * secs);
            if (min > .5f) { mood = "Happy"; img = "happy"; }
            else if (min > .25f) { mood = "Droopy"; img = "droopy"; }
            else if (min > .1f) { mood = "Flat: pop in soon"; img = "droopy"; }
            else { mood = "Needs you!"; img = "sad"; }
        }
        Context app = c.getApplicationContext();
        String pkg = app.getPackageName();
        int layout = app.getResources().getIdentifier("squishy_widget", "layout", pkg);
        RemoteViews v = new RemoteViews(pkg, layout);
        int imgId = app.getResources().getIdentifier("squishy_img", "id", pkg);
        String path = p.getString("img_" + img, null);
        if (path != null) {
            Bitmap b = BitmapFactory.decodeFile(path);
            if (b != null) v.setImageViewBitmap(imgId, b);
        }
        v.setTextViewText(app.getResources().getIdentifier("squishy_name", "id", pkg), name);
        v.setTextViewText(app.getResources().getIdentifier("squishy_mood", "id", pkg), mood);
        Intent open = app.getPackageManager().getLaunchIntentForPackage(pkg);
        if (open != null) {
            PendingIntent pi = PendingIntent.getActivity(app, 0, open, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
            v.setOnClickPendingIntent(app.getResources().getIdentifier("squishy_root", "id", pkg), pi);
        }
        m.updateAppWidget(id, v);
    }
}
