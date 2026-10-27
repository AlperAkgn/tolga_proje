START TRANSACTION;
ALTER TABLE `Demos` ADD `FileSha256` varchar(64) NOT NULL DEFAULT '';

ALTER TABLE `Demos` ADD `RightsConfirmedAtUtc` datetime(6) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261001201653_DemoFileIntegrity', '10.0.12');

COMMIT;

