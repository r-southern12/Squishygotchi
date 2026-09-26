package com.squishydumpling.widget;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Build;
import android.widget.RemoteViews;

/**
 * Picture-first reminders: the squishy itself, with a thought bubble of what it wants, fills the
 * notification (custom layout); the only text is its name and an emoji. Scheduled with AlarmManager
 * from Unity; nothing leaves the phone.
 */
public class SquishyNotify extends BroadcastReceiver {
    private static final String CHANNEL = "care";
    private static final int MAX_IDS = 8;

    public static void schedule(Context c, int id, long whenMillis, String title, String text, String imagePath) {
        Intent i = new Intent(c, SquishyNotify.class);
        i.putExtra("id", id);
        i.putExtra("title", title);
        i.putExtra("text", text);
        i.putExtra("img", imagePath);
        PendingIntent pi = PendingIntent.getBroadcast(c, id, i, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        AlarmManager am = (AlarmManager) c.getSystemService(Context.ALARM_SERVICE);
        if (am != null) am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, whenMillis, pi);
    }

    public static void cancelAll(Context c) {
        AlarmManager am = (AlarmManager) c.getSystemService(Context.ALARM_SERVICE);
        for (int id = 1; id <= MAX_IDS; id++) {
            Intent i = new Intent(c, SquishyNotify.class);
            PendingIntent pi = PendingIntent.getBroadcast(c, id, i, PendingIntent.FLAG_NO_CREATE | PendingIntent.FLAG_IMMUTABLE);
            if (pi != null && am != null) { am.cancel(pi); pi.cancel(); }
        }
        NotificationManager nm = (NotificationManager) c.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm != null) nm.cancelAll();
    }

    @Override
    public void onReceive(Context c, Intent intent) {
        Context app = c.getApplicationContext();
        NotificationManager nm = (NotificationManager) app.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null) return;
        if (Build.VERSION.SDK_INT >= 26 && nm.getNotificationChannel(CHANNEL) == null)
            nm.createNotificationChannel(new NotificationChannel(CHANNEL, "Care reminders", NotificationManager.IMPORTANCE_DEFAULT));
        String pkg = app.getPackageName();
        Bitmap bmp = null;
        String path = intent.getStringExtra("img");
        if (path != null) bmp = BitmapFactory.decodeFile(path);
        int imgId = app.getResources().getIdentifier("notif_img", "id", pkg);
        RemoteViews small = new RemoteViews(pkg, app.getResources().getIdentifier("squishy_notif_small", "layout", pkg));
        RemoteViews big = new RemoteViews(pkg, app.getResources().getIdentifier("squishy_notif_big", "layout", pkg));
        if (bmp != null) { small.setImageViewBitmap(imgId, bmp); big.setImageViewBitmap(imgId, bmp); }
        Intent open = app.getPackageManager().getLaunchIntentForPackage(pkg);
        PendingIntent tap = open == null ? null : PendingIntent.getActivity(app, 0, open, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        Notification.Builder b = Build.VERSION.SDK_INT >= 26 ? new Notification.Builder(app, CHANNEL) : new Notification.Builder(app);
        b.setSmallIcon(app.getResources().getIdentifier("ic_squishy", "drawable", pkg))
         .setContentTitle(intent.getStringExtra("title"))
         .setContentText(intent.getStringExtra("text"))
         .setAutoCancel(true);
        if (tap != null) b.setContentIntent(tap);
        if (bmp != null) {
            b.setStyle(new Notification.DecoratedCustomViewStyle());
            b.setCustomContentView(small);
            b.setCustomBigContentView(big);
            b.setLargeIcon(bmp);
        }
        nm.notify(intent.getIntExtra("id", 1), b.build());
    }
}
