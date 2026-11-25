"""Observe an unequipped Cry past the initial seven WARP preview frames."""

import time

import catalog_run as catalog


def cyan_tear_pixels(image):
    avatar = image.crop((10, 190, 535, 700))
    return sum(1 for r, g, b in avatar.getdata() if 30 < r < 180 and min(g, b) > 170 and min(g, b) - r > 60 and b > 180)


class ExtendedPlayer(catalog.Player):
    cry_started = 0
    cry_seen = False

    def cell(self, x=.50, y=.335):
        start = super().cell(x, y)
        if (x, y) == (.579, .335):
            self.cry_started = start
        return start

    def save_screen(self, label):
        image = super().save_screen(label)
        if label.startswith("unequipped-cry-"):
            self.observe(label, image, self.cry_started, catalog.CLOCK(), "first-selection")
            if label == "unequipped-cry-7" and not self.cry_seen:
                self.watch_selection("first-selection", self.cry_started, 180)
                if not self.cry_seen:
                    self.cell()
                    time.sleep(3)
                    self.save_screen("cry-reselection-after-clap")
                    start = self.cell(.579, .335)
                    self.watch_selection("reselection", start, 90)
        return image

    def observe(self, name, image, start, end, phase):
        pixels = cyan_tear_pixels(image)
        self.cry_seen |= pixels >= 100
        data = {"cry_phase":phase, "screenshot":name, "since_click_s":round(end - start, 3), "cyan_tear_pixels":pixels}
        catalog.record(**data)
        print("Cry observation", data, flush=True)
        return pixels

    def watch_selection(self, phase, start, deadline):
        next_checkpoint = 0
        while catalog.CLOCK() - start < deadline:
            _, end, image, raw = self.screenshot()
            elapsed = end - start
            pixels = cyan_tear_pixels(image)
            if pixels >= 100 or elapsed >= next_checkpoint or elapsed >= deadline - 8:
                name = f"cry-{phase}-{elapsed:07.2f}s"
                (catalog.OUT / (name + ".png")).write_bytes(raw)
                self.observe(name, image, start, end, phase)
                next_checkpoint = elapsed + 12
            if self.cry_seen:
                self.save_screen("cry-motion-confirmation-" + phase)
                return
            time.sleep(.35)

    def suite(self, log):
        super().suite(log)
        if not self.cry_seen:
            raise RuntimeError("No Cry tear pose was captured in either extended observation")


catalog.Player = ExtendedPlayer
catalog.main()
